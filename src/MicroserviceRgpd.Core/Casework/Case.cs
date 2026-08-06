using MicroserviceRgpd.Core.Casework.Adapters;
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
  private readonly List<Locating> _locatings;
  private readonly List<OpenQuestion> _questions;

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
    _locatings = [];
    _questions = [];
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private Case()
  {
    IdentityDeclaration = IdentityDeclaration.Unverified;
    Motivation = null;
    State = CaseState.Open;
    _designations = [];
    _claims = [];
    _locatings = [];
    _questions = [];
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
  /// Le sac de <see cref="Designation"/> — <b>la seule identité qui circule</b>. Il naît tel que le
  /// canal l'a déclaré et il <b>s'enrichit</b> en cours d'instruction : une réserve qu'un
  /// <c>Operator</c> rattache y verse ses désignations nouvelles, et l'appel <c>Locate</c> suivant
  /// les porte — confirmer une adresse trouvée en base ouvre le journal applicatif qui la cherchait
  /// sous une autre casse. Voir <see cref="Arbitrate"/>.
  /// </summary>
  public IReadOnlyList<Designation> Designations => _designations;

  /// <summary>
  /// Ce que les <c>Locate</c> ont rapporté, <b>un par <see cref="DeclaredSystem"/> appelé</b> —
  /// jamais par droit : un <c>Locate</c> cherche la personne, et non la réponse due sur un droit.
  /// </summary>
  public IReadOnlyList<Locating> Locatings => _locatings;

  /// <summary>
  /// Les questions ouvertes du dossier, datées. Elles <b>n'arrêtent jamais</b> le délai de
  /// l'art. 12.3 et ne barrent jamais la route à l'<c>Operator</c>.
  /// </summary>
  public IReadOnlyList<OpenQuestion> Questions => _questions;

  /// <summary>
  /// Le service détient-il un rattachement <b>où que ce soit</b> ? C'est la lecture dont naît la
  /// question de la désignation : six zéros ne doivent pas se lire « cette personne n'est pas chez
  /// nous ».
  /// </summary>
  public bool HoldsAnyAttachment => _locatings.Any(locating => locating.HoldsAnAttachment);

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
  /// Ce que le <c>Locate</c> a rapporté de ce système, ou <c>null</c> si on ne l'a pas encore appelé.
  /// <b>Le <c>null</c> n'est pas un zéro</b> : « pas appelé » et « appelé, rien trouvé » sont deux
  /// déclarations différentes, et c'est la seconde qui a une valeur de preuve.
  /// </summary>
  public Locating? LocatingIn(DeclaredSystemId declaredSystem)
  {
    return _locatings.SingleOrDefault(locating => locating.DeclaredSystem == declaredSystem);
  }

  /// <summary>Le service détient-il un rattachement dans ce système ?</summary>
  public bool HoldsAnAttachmentIn(DeclaredSystemId declaredSystem)
  {
    return LocatingIn(declaredSystem)?.HoldsAnAttachment ?? false;
  }

  /// <summary>
  /// Un <b>constat</b> est-il réclamé pour déclarer ce travail dû dans cet état ? C'est la règle de
  /// <see cref="StepState.DemandsAFinding"/>, lue avec les rattachements que ce dossier détient.
  /// </summary>
  /// <remarks>
  /// <b>Elle vit ici parce qu'elle a besoin des deux moitiés</b> — l'état, qui est du <c>Step</c>, et
  /// les rattachements, qui sont du dossier. L'écran s'en sert pour <b>réclamer</b>, la ligne de
  /// preuve pour <b>refuser</b> une déclaration que personne n'a motivée : une seule règle, aux deux
  /// endroits.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="state"/> est absent.</exception>
  public bool FindingIsDemandedBy(StepState state, DeclaredSystemId declaredSystem)
  {
    ArgumentNullException.ThrowIfNull(state);

    return state.DemandsAFinding(HoldsAnAttachmentIn(declaredSystem));
  }

  /// <summary>
  /// Un <c>Adapter</c> a <b>servi</b> un <c>Locate</c> : le dossier retient le noyau certain et les
  /// réserves, et rend la localisation telle qu'elle se lit désormais.
  /// </summary>
  /// <remarks>
  /// <b>Elle ne consigne rien.</b> La ligne de preuve est écrite par l'appelant, hors de l'agrégat —
  /// et c'est lui, et lui seul, qui décide de ne pas consigner un verdict identique au précédent : le
  /// <c>Ledger</c> ne se relit jamais.
  /// </remarks>
  /// <param name="declaredSystem">Le système qu'on a interrogé.</param>
  /// <param name="findings">Ce qu'il a rendu — <see cref="LocateFindings.Nothing"/> compris.</param>
  /// <param name="askedAt">L'instant de la tentative.</param>
  /// <exception cref="ArgumentNullException"><paramref name="findings"/> est absent.</exception>
  public Locating LocateServed(DeclaredSystemId declaredSystem, LocateFindings findings, DateTimeOffset askedAt)
  {
    ArgumentNullException.ThrowIfNull(findings);

    var locating = LocatingFor(declaredSystem, askedAt.ToUniversalTime());

    locating.Served(findings.Certain, findings.Reserved, askedAt.ToUniversalTime(), _designations.Count);

    return locating;
  }

  /// <summary>
  /// Un <c>Adapter</c> a <b>différé</b> et déclaré son échéance. Rien du rattachement ne bouge : le
  /// service repassera après elle, <b>à l'ouverture du dossier</b> et jamais depuis la file.
  /// </summary>
  public Locating LocateDeferred(
    DeclaredSystemId declaredSystem,
    DateTimeOffset declaredDeadline,
    DateTimeOffset askedAt)
  {
    var locating = LocatingFor(declaredSystem, askedAt.ToUniversalTime());

    locating.Deferred(declaredDeadline.ToUniversalTime(), askedAt.ToUniversalTime(), _designations.Count);

    return locating;
  }

  /// <summary>
  /// Un <c>Adapter</c> a <b>refusé</b>. Rien du rattachement ne bouge : un refus dit que le service
  /// et l'application ne sont pas d'accord, jamais ce que le système porte.
  /// </summary>
  /// <exception cref="ArgumentException">La réponse donnée n'est pas un refus.</exception>
  public Locating LocateRefused(DeclaredSystemId declaredSystem, AdapterOutcome refusal, DateTimeOffset askedAt)
  {
    var locating = LocatingFor(declaredSystem, askedAt.ToUniversalTime());

    locating.Refused(refusal, askedAt.ToUniversalTime(), _designations.Count);

    return locating;
  }

  /// <summary>
  /// Un humain <b>tranche une réserve</b>, et le sac s'enrichit de ce qu'elle proposait si elle est
  /// rattachée.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Aucun rattachement n'est décidé par le service.</b> Ni la fusion à tort — irréversible, et
  /// portant sur la donnée d'un tiers — ni l'exclusion par prudence, qui est l'<c>Omission
  /// silencieuse</c>. Cette méthode ne fait que porter l'issue d'un humain, que l'appelant datera et
  /// signera au <c>Ledger</c>.
  /// </para>
  /// <para>
  /// <b>Seules les <see cref="Designation"/> entrent au sac, et seulement sur un rattachement.</b> La
  /// <see cref="OpaqueReference"/>, elle, n'a de sens que chez le client : une réserve qui n'apporte
  /// qu'elle reste <b>locale et opaque</b>, et le service n'apprendra jamais ce qu'elle désignait.
  /// </para>
  /// <para>
  /// <b>Elle rend <c>null</c> plutôt qu'elle ne lève</b> quand la réserve est inconnue ou déjà
  /// tranchée : un écran affiché il y a une minute peut nommer une réserve qu'un autre geste vient
  /// d'arbitrer, et ce n'est pas une programmation fautive.
  /// </para>
  /// </remarks>
  /// <param name="declaredSystem">Le système où la réserve a été levée.</param>
  /// <param name="reference">La référence opaque qui identifie la réserve dans ce système.</param>
  /// <param name="ruling">L'issue de l'humain : rattachée, ou écartée.</param>
  /// <returns>La réserve tranchée, ou <c>null</c> si aucune ne l'attendait.</returns>
  /// <exception cref="ArgumentNullException">Un argument obligatoire est absent.</exception>
  /// <exception cref="ArgumentException"><paramref name="ruling"/> n'est pas un arbitrage.</exception>
  public Reservation? Arbitrate(DeclaredSystemId declaredSystem, OpaqueReference reference, ReservationState ruling)
  {
    ArgumentNullException.ThrowIfNull(reference);
    ArgumentNullException.ThrowIfNull(ruling);

    var arbitrated = LocatingIn(declaredSystem)?.Arbitrate(reference, ruling);

    if (arbitrated is null || ruling != ReservationState.Attached)
    {
      return arbitrated;
    }

    foreach (var designation in arbitrated.Designations)
    {
      // Ce qui est déjà au sac n'y entre pas deux fois : le compte du Ledger mesure l'ampleur d'une
      // recherche, et un doublon la gonflerait sans qu'aucune porte de plus ne s'ouvre.
      if (!_designations.Contains(designation))
      {
        _designations.Add(designation);
      }
    }

    return arbitrated;
  }

  /// <summary>
  /// Pose une question ouverte sur le dossier, <b>datée</b>, et dit si elle ne l'était pas déjà.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Elle ne se pose qu'une fois.</b> Reposer la même question à chaque passage ferait d'une
  /// question ouverte un bruit quotidien, et la date qu'elle porte — la seule chose que l'écran en
  /// dise — se réinitialiserait à chaque regard.
  /// </para>
  /// <para>
  /// <b>Elle n'arrête rien et ne barre rien</b> : le délai de l'art. 12.3 court pendant qu'elle
  /// attend, et l'<c>Operator</c> instruit le dossier comme avant.
  /// </para>
  /// </remarks>
  /// <returns><c>true</c> si la question vient d'être posée ; <c>false</c> si elle l'était déjà.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="subject"/> est absent.</exception>
  public bool Ask(OpenQuestionSubject subject, DateTimeOffset askedOn)
  {
    ArgumentNullException.ThrowIfNull(subject);

    if (_questions.Any(question => question.Subject == subject))
    {
      return false;
    }

    _questions.Add(new OpenQuestion(subject, askedOn.ToUniversalTime()));

    return true;
  }

  /// <summary>La localisation de ce système, posée si elle n'existait pas encore.</summary>
  private Locating LocatingFor(DeclaredSystemId declaredSystem, DateTimeOffset askedAt)
  {
    var known = LocatingIn(declaredSystem);

    if (known is not null)
    {
      return known;
    }

    var opened = new Locating(declaredSystem, askedAt, _designations.Count);

    _locatings.Add(opened);

    return opened;
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
