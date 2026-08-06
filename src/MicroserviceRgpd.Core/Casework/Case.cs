using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Le dossier ouvert pour une demande d'exercice de droits, et <b>l'agrégat de ce contexte</b>. Il
/// porte l'identité déclarée du demandeur, les <see cref="Designations"/> sous lesquelles on le
/// cherche, la date de réception, et les <see cref="Claim"/> qu'on lui reconnaît.
/// <para>
/// <b>Une demande, un <c>Case</c> — jamais deux</b>, quels que soient les droits qu'elle porte. La
/// règle « lire avant d'effacer » traverse les <see cref="Claim"/> et aucune frontière plus fine ne
/// peut la tenir : elle ne s'exercera qu'au lot où <c>Erasure</c> arrive, mais la frontière se pose
/// maintenant — la déplacer plus tard reviendrait à la déplacer sur des dossiers déjà écrits.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Racine unique, et seule racine.</b> Il n'existe aucun dépôt de <see cref="Claim"/> ni de
/// <see cref="Step"/>, et aucun chemin d'écriture vers eux hors d'ici : les règles sont écrites une
/// fois, sur la racine. La contention est assumée — quelques demandes par an rendent le troc
/// évident.
/// </para>
/// <para>
/// <b>Hors de l'agrégat</b> : le <c>Ledger</c>, en ajout seul et survivant au dossier, et les
/// <c>RetrievedData</c>, qui ont leur durée de vie propre. <b>Dedans</b> : ce qui meurt avec le
/// dossier.
/// </para>
/// <para>
/// <b>Il n'y a pas d'état « en retard ».</b> Le dépassement du délai de l'art. 12.3 est un
/// <b>calcul</b> fait sur <see cref="Reception"/> à l'instant où l'<c>Operator</c> regarde : un
/// état persisté ferait dépendre la preuve de ce qu'une minuterie ait tourné, et un retard non
/// détecté deviendrait un retard inexistant.
/// </para>
/// </remarks>
public sealed class Case : IAggregateRoot
{
  private readonly List<Designation> _designations;
  private readonly List<Claim> _claims;

  private Case(
    CaseId id,
    IdentityDeclaration identityDeclaration,
    IdentityMotivation? motivation,
    ReceptionDate reception,
    List<Designation> designations,
    List<Claim> claims)
  {
    Id = id;
    IdentityDeclaration = identityDeclaration;
    Motivation = motivation;
    Reception = reception;
    State = CaseState.Open;
    _designations = designations;
    _claims = claims;
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private Case()
  {
    IdentityDeclaration = IdentityDeclaration.Unverified;
    Motivation = null;
    State = CaseState.Open;
    _designations = [];
    _claims = [];
  }

  /// <summary>L'identité que le service donne à ce dossier, engendrée à son ouverture.</summary>
  public CaseId Id { get; private set; }

  /// <summary>
  /// Ce que le canal d'entrée a déclaré sur l'identité du demandeur. <b>Portée par le dossier</b> :
  /// l'identité est une propriété de la personne, jamais d'un droit.
  /// </summary>
  public IdentityDeclaration IdentityDeclaration { get; private set; }

  /// <summary>
  /// Ce que l'humain a pesé avant d'ouvrir un droit sous cette identité — une méthode qui se compte,
  /// un détail en prose qui meurt ici — ou <c>null</c> quand <b>personne ne l'a pesé</b>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Le <c>null</c> n'est pas <see cref="IdentityVerificationMethod.None"/>.</b> <c>None</c>
  /// est une réponse — quelqu'un a regardé et dit qu'il n'avait rien fait ; le <c>null</c> est
  /// l'absence de réponse. Les confondre reviendrait à faire signer par défaut un aveu que personne
  /// n'a écrit, exactement comme une date de réception tenue pour défaut se confondrait avec une date
  /// déclarée. Voir <see cref="AwaitsAMotivation"/>.
  /// </para>
  /// <para>
  /// <b>Elle est portée par le dossier</b>, comme la <see cref="IdentityDeclaration"/> qu'elle
  /// motive : l'identité est une propriété de la personne, jamais d'un droit.
  /// </para>
  /// </remarks>
  public IdentityMotivation? Motivation { get; private set; }

  /// <summary>
  /// Le jour où le responsable de traitement a reçu la demande — <b>déclaré, jamais constaté</b> —
  /// et le régime sous lequel le service le sait : affirmé par quelqu'un, ou tenu pour défaut.
  /// Le mois de l'art. 12.3 court avant nous et rien ne l'arrête : le service hérite d'un compteur
  /// lancé depuis un nombre de jours qu'il ignore, et c'est de cette date que tout se recalcule.
  /// </summary>
  public ReceptionDate Reception { get; private set; } = null!;

  /// <summary>
  /// Où en est le dossier. <b>Deux valeurs, et jamais une troisième nommée « en retard »</b> : le
  /// dépassement est un <see cref="StatutoryDeadline"/> calculé à l'affichage.
  /// </summary>
  public CaseState State { get; private set; }

  /// <summary>
  /// Le sac de <see cref="Designation"/> — <b>la seule identité qui circule</b>. Il s'enrichira en
  /// cours d'instruction, lorsqu'une réserve confirmée par l'<c>Operator</c> y versera une
  /// désignation nouvelle ; ce geste appartient à l'arbitrage du <c>Locate</c>, et le sac naît ici
  /// tel que le canal l'a déclaré.
  /// </summary>
  public IReadOnlyList<Designation> Designations => _designations;

  /// <summary>
  /// Les droits qu'on reconnaît à cette demande, un <see cref="Claim"/> chacun. <b>Éventuellement
  /// vide</b> : une demande n'exerçant aucun droit entre quand même, et un dossier vide de
  /// réclamations est un fait, jamais une saisie inachevée.
  /// </summary>
  public IReadOnlyList<Claim> Claims => _claims;

  /// <summary>
  /// Le dossier <b>réclame-t-il encore une motivation</b> que personne n'a écrite ? Vrai lorsqu'au
  /// moins un droit l'exige et que <see cref="Motivation"/> est absente.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>C'est une lecture, et surtout pas un état.</b> Rien ne s'est ajouté au dossier pour porter
  /// cette exigence : elle se recalcule à chaque affichage à partir de ce que les <c>Claim</c>
  /// portent déjà. Un état persisté aurait fait dépendre la visibilité d'une faiblesse de ce que
  /// quelqu'un ait pensé à le poser.
  /// </para>
  /// <para>
  /// <b>Elle ne barre rien, et c'est tout son propos.</b> Le dossier est ouvert, le délai court, et
  /// l'exigence non satisfaite reste <b>affichée</b> tant qu'elle ne l'est pas : la faiblesse d'un
  /// dossier doit rester visible plutôt que contournée. Un refus à l'entrée l'aurait fait disparaître
  /// — soit en renvoyant la personne à son silence, soit en faisant cocher n'importe quoi.
  /// </para>
  /// </remarks>
  public bool AwaitsAMotivation => Motivation is null && _claims.Any(claim => claim.MotivationIsDemanded);

  /// <summary>
  /// Un humain écrit <b>après coup</b> ce qu'il a pesé de l'identité du demandeur, et dit si le
  /// dossier réclamait encore une motivation.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Elle existe pour que la réclamation puisse être satisfaite.</b> Sans elle, un dossier déposé
  /// sans motivation la réclamerait pour toujours, et une exigence qu'on ne peut pas satisfaire cesse
  /// d'être lue : c'est très exactement l'écran « ✅ demande traitée » à l'envers — un bandeau rouge
  /// permanent qu'on apprend à ne plus voir.
  /// </para>
  /// <para>
  /// ⚠️ <b>Elle ne touche à aucun <see cref="Claim"/>, et c'est tout le propos du gel.</b> Le droit
  /// ouvert lundi garde l'<see cref="Claim.IdentityAtOrigin"/> sous laquelle il est né ; la motivation
  /// écrite vendredi dit ce qu'on a fini par peser, elle ne rend pas rétroactivement propre ce qui a
  /// été fait sur la foi de rien. La preuve d'hier ne se corrige pas par la saisie d'aujourd'hui.
  /// </para>
  /// <para>
  /// <b>Elle rend faux plutôt qu'elle ne lève</b> quand rien n'était réclamé : ce n'est pas une
  /// programmation fautive mais un écran affiché avant qu'un autre geste ne satisfasse la demande, et
  /// écraser une motivation déjà écrite ferait réécrire ce que quelqu'un a signé.
  /// </para>
  /// <para>
  /// <b>Elle ne consigne rien.</b> La ligne de preuve est écrite par l'appelant, hors de l'agrégat.
  /// </para>
  /// </remarks>
  /// <param name="motivation">Ce que l'humain a pesé, méthode et détail pris ensemble.</param>
  /// <returns><c>true</c> si le dossier réclamait une motivation ; <c>false</c> sinon, sans rien changer.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="motivation"/> est absent.</exception>
  public bool DeclareMotivation(IdentityMotivation motivation)
  {
    ArgumentNullException.ThrowIfNull(motivation);

    if (!AwaitsAMotivation)
    {
      return false;
    }

    Motivation = motivation;

    return true;
  }

  /// <summary>
  /// Un humain <b>reprend à son compte</b> un droit qu'une <c>Qualification</c> avait seulement
  /// proposé, et dit s'il en existait un à confirmer.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>La confirmation a lieu ici, dans le dossier ouvert</b>, pendant que le délai de l'art. 12.3
  /// court. Il n'existe aucun vestibule où une demande attendrait d'être confirmée avant d'entrer :
  /// le compteur ne s'arrête pas, et une salle d'attente aurait fait passer pour « pas encore
  /// commencé » un mois déjà entamé.
  /// </para>
  /// <para>
  /// <b>C'est la racine qui écrit, et elle seule</b> — comme <see cref="Declare"/>, et pour la même
  /// raison. <b>Elle ne consigne rien</b> : la ligne de preuve est écrite par l'appelant, hors de
  /// l'agrégat.
  /// </para>
  /// <para>
  /// <b>Elle rend faux plutôt qu'elle ne lève</b> quand le dossier ne porte pas ce droit : un écran
  /// affiché il y a une minute peut nommer un droit qu'un autre geste vient de changer, et ce n'est
  /// pas une programmation fautive.
  /// </para>
  /// </remarks>
  /// <param name="right">Le droit qu'un humain reprend à son compte.</param>
  /// <returns>
  /// <c>true</c> si le dossier portait ce droit et qu'il attendait d'être confirmé ; <c>false</c>
  /// sinon, sans rien changer.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="right"/> est absent.</exception>
  public bool Confirm(DataSubjectRight right)
  {
    ArgumentNullException.ThrowIfNull(right);

    var claim = _claims.SingleOrDefault(one => one.Right == right);

    // Rien à confirmer se dit faux, et non vrai : le Ledger consigne les faits qui CHANGENT quelque
    // chose, jamais leur répétition, et cette règle est tenue par l'appelant. Rendre vrai sur un
    // droit déjà confirmé lui ferait écrire une seconde ligne identique — du bruit de mécanique dans
    // ce que le contrôle vient lire.
    if (claim is null || !claim.AwaitsConfirmation)
    {
      return false;
    }

    claim.Confirm();

    return true;
  }

  /// <summary>
  /// Ouvre le dossier d'une demande : <b>un seul</b>, quels que soient les droits qu'elle porte, et
  /// un <see cref="Step"/> par (<see cref="Claim"/>, <see cref="DeclaredSystem"/>) du catalogue tel
  /// qu'il se lit à cet instant.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Rien n'est deviné, rien n'est complété.</b> Les droits arrivent tels qu'ils ont été
  /// reconnus, les désignations telles qu'elles ont été déclarées, et la date de réception telle
  /// qu'elle a été dite.
  /// </para>
  /// <para>
  /// <b>Le catalogue est lu une fois, ici.</b> Un système déclaré demain n'ajoutera aucun
  /// <see cref="Step"/> à ce dossier — ce qui est le prix assumé d'un <c>Manifest</c> qui vieillit
  /// exprès, et non un oubli : le <c>Step</c> atteste le travail dû tel qu'on le savait dû quand le
  /// dossier s'est ouvert.
  /// </para>
  /// </remarks>
  /// <param name="id">L'identité du dossier.</param>
  /// <param name="identityDeclaration">Ce que le canal d'entrée a déclaré de l'identité du demandeur.</param>
  /// <param name="motivation">
  /// Ce que l'humain a pesé avant d'ouvrir ces droits sous cette identité, ou <c>null</c> si personne
  /// ne l'a pesé. <b>Le <c>null</c> n'est jamais refusé</b> : le service ne barre pas la route, et un
  /// dossier faible reste enregistrable et <b>visible comme tel</b> — voir
  /// <see cref="AwaitsAMotivation"/>.
  /// </param>
  /// <param name="designations">Le sac sous lequel on cherchera la personne, éventuellement vide.</param>
  /// <param name="rights">
  /// Les droits reconnus. Les doublons se fondent — deux fois le même droit est une seule
  /// réclamation, pas deux réponses dues.
  /// </param>
  /// <param name="origin">
  /// D'où vient la reconnaissance de ces droits. <b>Une seule par ouverture</b> : les droits d'un
  /// dépôt arrivent tous par la même porte, et l'origine se fige sur chaque <see cref="Claim"/>.
  /// </param>
  /// <param name="manifest">Le paysage déclaré du client, tel qu'il se lit à cet instant.</param>
  /// <param name="reception">
  /// Le jour de réception par le responsable de traitement, et la façon dont le service le sait.
  /// </param>
  /// <exception cref="ArgumentNullException">Un argument obligatoire est absent.</exception>
  /// <exception cref="ArgumentException">
  /// <see cref="DataSubjectRight.OutOfScope"/> figure parmi les droits : ce n'est pas un droit
  /// réclamable, c'est le verdict qu'aucun ne l'a été.
  /// </exception>
  public static Case Open(
    CaseId id,
    IdentityDeclaration identityDeclaration,
    IdentityMotivation? motivation,
    IEnumerable<Designation> designations,
    IEnumerable<DataSubjectRight> rights,
    ClaimOrigin origin,
    Manifest manifest,
    ReceptionDate reception)
  {
    ArgumentNullException.ThrowIfNull(identityDeclaration);
    ArgumentNullException.ThrowIfNull(designations);
    ArgumentNullException.ThrowIfNull(rights);
    ArgumentNullException.ThrowIfNull(origin);
    ArgumentNullException.ThrowIfNull(manifest);
    ArgumentNullException.ThrowIfNull(reception);

    var claimed = Claimable(rights);

    // Les systèmes dans l'ordre du catalogue, lus une seule fois : chaque Claim reçoit le même
    // travail dû, et deux dossiers ouverts sur le même paysage se relisent dans le même ordre.
    var declaredSystems = manifest.Systems.Select(system => system.Id).ToArray();

    return new Case(
      id,
      identityDeclaration,
      motivation,
      reception,
      Bag(designations),
      // L'identité déclarée descend sur chaque Claim, où elle se fige : le dossier porte celle
      // d'aujourd'hui, le droit garde celle sous laquelle il s'est ouvert.
      [.. claimed.Select(right => new Claim(right, origin, identityDeclaration, declaredSystems))]);
  }

  /// <summary>
  /// Porte sur un <see cref="Step"/> l'état que l'<c>Operator</c> vient de <b>déclarer</b>, et dit
  /// si le dossier connaissait ce travail dû.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>C'est la racine qui écrit, et elle seule.</b> Le chemin passe par ici parce qu'aucun dépôt de
  /// <see cref="Claim"/> ni de <see cref="Step"/> n'existe : les règles sont écrites une fois, sur
  /// l'agrégat.
  /// </para>
  /// <para>
  /// <b>Elle ne consigne rien.</b> La ligne de preuve est écrite par l'appelant, hors de l'agrégat :
  /// le <c>Ledger</c> survit au dossier de cinq ans, et le faire écrire d'ici l'aurait attaché à la
  /// durée de vie de ce qu'il doit précisément survivre.
  /// </para>
  /// <para>
  /// <b>Elle rend faux plutôt qu'elle ne lève</b> lorsque la paire est inconnue du dossier. Un
  /// travail dû qui n'existe pas ici n'est pas une programmation fautive : le <c>Manifest</c>
  /// vieillit exprès, et un système déclaré après l'ouverture n'a jamais eu de <c>Step</c> dans ce
  /// dossier — c'est un fait que l'écran doit pouvoir dire.
  /// </para>
  /// </remarks>
  /// <param name="right">Le droit dont on déclare le travail dû.</param>
  /// <param name="declaredSystem">Le système sur lequel ce travail était dû.</param>
  /// <param name="state">L'état déclaré.</param>
  /// <returns><c>true</c> si le dossier portait ce travail dû ; <c>false</c> sinon, sans rien changer.</returns>
  /// <exception cref="ArgumentNullException">Un argument obligatoire est absent.</exception>
  public bool Declare(DataSubjectRight right, DeclaredSystemId declaredSystem, StepState state)
  {
    ArgumentNullException.ThrowIfNull(right);
    ArgumentNullException.ThrowIfNull(state);

    var step = _claims
      .SingleOrDefault(claim => claim.Right == right)?
      .Steps
      .SingleOrDefault(one => one.DeclaredSystem == declaredSystem);

    if (step is null)
    {
      return false;
    }

    step.Declare(state);

    return true;
  }

  /// <summary>
  /// Les droits réclamables, sans doublon et dans l'ordre de la taxonomie.
  /// </summary>
  /// <remarks>
  /// L'ordre est celui de la taxonomie plutôt que celui de la saisie : deux demandes portant les
  /// mêmes droits s'écrivent alors pareil, en base comme à l'écran, et personne n'a besoin d'un
  /// ordre instable pour se convaincre qu'un ensemble n'en a pas.
  /// </remarks>
  private static DataSubjectRight[] Claimable(IEnumerable<DataSubjectRight> rights)
  {
    var claimed = new HashSet<DataSubjectRight>(rights);

    if (claimed.Contains(DataSubjectRight.OutOfScope))
    {
      throw new ArgumentException(
        "OutOfScope n'est pas un droit réclamable : c'est le verdict qu'aucun droit n'a été "
        + "reconnu. Une demande n'exerçant aucun droit ouvre un Case sans Claim, et se clôt "
        + "NotApplicable sous la signature d'un humain.",
        nameof(rights));
    }

    return [.. claimed.OrderBy(right => right.Value)];
  }

  /// <summary>Le sac à l'ouverture : dans l'ordre déclaré, et sans qu'une désignation y figure deux fois.</summary>
  private static List<Designation> Bag(IEnumerable<Designation> designations)
  {
    var bag = new List<Designation>();

    foreach (var designation in designations)
    {
      ArgumentNullException.ThrowIfNull(designation);

      if (!bag.Contains(designation))
      {
        bag.Add(designation);
      }
    }

    return bag;
  }
}
