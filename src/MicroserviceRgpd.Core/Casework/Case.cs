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
/// <b>Hors de l'agrégat</b> : l'<c>EvidenceLog</c>, en ajout seul et survivant au dossier, et les
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
  private readonly List<Reading> _readings;
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
    _readings = [];
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
    _readings = [];
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
  /// Ce que les <c>Read</c> ont tenté, <b>un par (<see cref="Claim"/>, <see cref="DeclaredSystem"/>)
  /// appelé</b> — et rien de ce qu'ils ont ramené : les pièces vivent dans des
  /// <see cref="RetrievedData"/>, hors de l'agrégat, avec leur durée de vie propre.
  /// </summary>
  public IReadOnlyList<Reading> Readings => _readings;

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
  /// Une réserve attend-elle un humain, <b>où que ce soit</b> ? C'est l'autre moitié de la lecture
  /// dont naît la question de la désignation, et elle en est la négation : un dossier qui a du
  /// travail posé devant quelqu'un n'a pas de question ouverte à poser.
  /// </summary>
  /// <remarks>
  /// Une réserve n'est pas un rattachement — personne ne l'a tranchée — mais elle n'est pas un zéro
  /// non plus : quelque chose a été trouvé, et ce qui manque est un regard, pas une désignation.
  /// Les confondre ferait afficher « on n'a rien trouvé sous ce qu'on a » au-dessus de deux lignes
  /// que le système vient précisément de trouver.
  /// </remarks>
  public bool AwaitsAnArbitration => _locatings.Any(locating => locating.AwaitsAnArbitration);

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
  /// Un paquet est-il sorti du service sans que personne n'ait déclaré la remise ? Vrai dès qu'un
  /// droit est dans ce cas.
  /// </summary>
  /// <remarks>
  /// <b>C'est une lecture, jamais un état.</b> Elle se recalcule sur les <see cref="Claim"/> à chaque
  /// affichage, et sert de <b>colonne</b> sur une ligne déjà présente du tableau des demandes
  /// RGPD : une remise commencée
  /// et non déclarée doit se voir tous les jours, plutôt que de manquer.
  /// </remarks>
  public bool AwaitsADeliveryDeclaration => _claims.Any(claim => claim.DeliveryAwaitsDeclaration);

  /// <summary>
  /// Ce par quoi le dossier s'est clos, ou <c>null</c> tant qu'il est ouvert. <b>Toujours nommée par
  /// un humain</b> : la machine ne produit aucune issue.
  /// </summary>
  public ClosingCause? ClosingCause { get; private set; }

  /// <summary>
  /// L'instant de la clôture, ou <c>null</c> tant que le dossier est ouvert. <b>C'est aussi
  /// l'instant où tout le nominatif a été détruit</b> : il n'y a pas deux dates, parce qu'il n'y a
  /// pas deux gestes.
  /// </summary>
  /// <remarks>
  /// C'est de lui que court la vie de l'<c>EvidenceLog</c> — cinq ans à compter de la clôture — et c'est
  /// pourquoi il est <b>porté par le dossier</b> plutôt que recalculé depuis la preuve : le dossier
  /// clos est ce qu'un humain relit, la preuve est ce que le contrôle relit.
  /// </remarks>
  public DateTimeOffset? ClosedOn { get; private set; }

  /// <summary>
  /// La prolongation de l'art. 12.3 déclarée sur ce dossier, ou <c>null</c> si personne n'en a
  /// déclaré. <b>Le dossier la garde, il ne la juge pas</b> : c'est <see cref="StatutoryDeadline"/>
  /// qui dit, à l'instant où quelqu'un regarde, si elle déplace l'échéance ou non.
  /// </summary>
  public ExtensionDeclaration? ExtensionDeclaration { get; private set; }

  /// <summary>Le dossier est-il clos ? La lecture que tout geste d'écriture consulte avant d'agir.</summary>
  public bool IsClosed => State == CaseState.Closed;

  /// <summary>
  /// Les droits sur lesquels <b>personne n'a encore rendu d'issue</b>. C'est ce que la clôture
  /// <b>réclame</b>, et jamais ce qu'elle exige.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Une lecture, jamais un état.</b> Elle se recalcule sur les <see cref="Claim"/> à chaque
  /// affichage : l'écran la montre avant de laisser signer, et un dossier se clôt malgré elle.
  /// </para>
  /// <para>
  /// ⚠️ <b>Elle ne barre rien.</b> Un <c>Claim</c> resté ouvert dans un dossier clos est un fait
  /// que la preuve garde et que la relecture voit — pas une saisie qu'il faudrait forcer.
  /// </para>
  /// </remarks>
  public IReadOnlyList<Claim> ClaimsAwaitingAnOutcome =>
    [.. _claims.Where(claim => claim.AwaitsAnOutcome)];

  /// <summary>
  /// Les travaux dus dont <b>personne n'a dit où ils en étaient</b> — voir
  /// <see cref="StepState.WasDeclared"/>. L'autre moitié de ce que la clôture réclame.
  /// </summary>
  /// <remarks>
  /// <b>Elle ne barre rien non plus.</b> Un <c>Step</c> laissé <see cref="StepState.ToDo"/> dans un
  /// dossier clos <b>reste</b> <c>ToDo</c>, et se lit comme l'oubli qu'il est : c'est la seule trace
  /// que l'<c>Omission silencieuse</c> laisse jamais, et la clôture n'a pas le droit de la couvrir.
  /// </remarks>
  public IReadOnlyList<Step> StepsAwaitingADeclaration =>
    [.. _claims.SelectMany(claim => claim.Steps).Where(step => !step.State.WasDeclared)];

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

    // Un dossier clos n'a plus de détail à recevoir : celui qu'il portait vient d'être détruit, et
    // en écrire un nouveau le lendemain rendrait au dossier la prose nominative qu'on lui a ôtée.
    if (IsClosed || !AwaitsAMotivation)
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

    // Rien à confirmer se dit faux, et non vrai : l'EvidenceLog consigne les faits qui CHANGENT quelque
    // chose, jamais leur répétition, et cette règle est tenue par l'appelant. Rendre vrai sur un
    // droit déjà confirmé lui ferait écrire une seconde ligne identique — du bruit de mécanique dans
    // ce que le contrôle vient lire.
    if (IsClosed || claim is null || !claim.AwaitsConfirmation)
    {
      return false;
    }

    claim.Confirm();

    return true;
  }

  /// <summary>
  /// Le paquet d'un droit <b>sort du service</b> — premier des deux gestes de la remise.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Rien n'est remis ici, et rien ne se consigne.</b> Ce geste sert à ouvrir le ZIP et à vérifier
  /// qu'il n'est pas vide ; dater la preuve maintenant la daterait du moment où un fichier a quitté
  /// un serveur, alors que la remise est une affirmation.
  /// </para>
  /// <para>
  /// <b>Une remise déjà déclarée ne se reprend pas.</b> Les <see cref="RetrievedData"/> ont été
  /// détruites par le second geste : le paquet n'existe plus, et prétendre le tendre à nouveau
  /// rendrait une page de garde sans une seule des pièces qu'elle annonce.
  /// </para>
  /// </remarks>
  /// <param name="right">Le droit dont on prend le paquet.</param>
  /// <param name="takenOn">L'instant où le paquet sort.</param>
  /// <returns>
  /// <c>true</c> si le dossier porte ce droit et que la remise n'est pas déclarée ; <c>false</c>
  /// sinon, sans rien changer.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="right"/> est absent.</exception>
  public bool TakeDelivery(DataSubjectRight right, DateTimeOffset takenOn)
  {
    ArgumentNullException.ThrowIfNull(right);

    var claim = _claims.SingleOrDefault(one => one.Right == right);

    if (IsClosed || claim is null || claim.DeliveryDeclaredOn is not null)
    {
      return false;
    }

    // Le premier instant est gardé, et reprendre le paquet reste permis : ce qui compte est qu'un
    // exemplaire soit dehors depuis ce jour-là, et non combien de fois on l'a copié.
    claim.TakeDelivery(takenOn);

    return true;
  }

  /// <summary>
  /// Un humain <b>déclare la remise</b> d'un droit — second geste, et le seul qui date la remise.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Elle ne se déclare qu'après que le paquet est sorti.</b> Déclarer remis un paquet que
  /// personne n'a jamais tenu daterait la remise de quelque chose dont il n'existe aucun exemplaire —
  /// une preuve d'un geste qui n'a pas eu lieu. Ce n'est pas barrer la route à l'<c>Operator</c> :
  /// le premier geste est à un clic, et il ne coûte rien.
  /// </para>
  /// <para>
  /// <b>Elle ne détruit rien elle-même.</b> Les <see cref="RetrievedData"/> vivent hors de l'agrégat,
  /// et c'est l'appelant qui les efface — la remise détruit la pièce <b>sans réécrire le dossier</b>.
  /// Elle ne consigne rien non plus : la ligne de preuve est écrite hors de l'agrégat, comme partout.
  /// </para>
  /// </remarks>
  /// <param name="right">Le droit dont on déclare la remise.</param>
  /// <param name="declaredOn">L'instant de la déclaration.</param>
  /// <returns>
  /// <c>true</c> si la remise vient d'être déclarée ; <c>false</c> si le dossier ne porte pas ce
  /// droit, si aucun paquet n'est sorti, ou si elle l'était déjà.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="right"/> est absent.</exception>
  public bool DeclareDelivered(DataSubjectRight right, DateTimeOffset declaredOn)
  {
    ArgumentNullException.ThrowIfNull(right);

    var claim = _claims.SingleOrDefault(one => one.Right == right);

    if (IsClosed || claim is null || claim.DeliveryTakenOn is null)
    {
      return false;
    }

    return claim.DeclareDelivered(declaredOn);
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
  /// l'<c>EvidenceLog</c> survit au dossier de cinq ans, et le faire écrire d'ici l'aurait attaché à la
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

    // Un dossier clos ne se travaille plus : déclarer un travail dû après coup daterait un geste que
    // plus aucune donnée du dossier ne soutient — les désignations sous lesquelles on cherchait ont
    // été détruites. Ce qui est resté ToDo doit le rester, et se lire comme l'oubli qu'il est.
    if (IsClosed || step is null)
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
  /// <c>EvidenceLog</c> ne se relit jamais.
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
  /// service repassera après elle, <b>à l'ouverture du dossier</b> et jamais depuis le tableau des
  /// demandes RGPD.
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
  /// Ce que le <c>Read</c> a tenté sur ce système au titre de ce droit, ou <c>null</c> si on ne l'a
  /// pas encore appelé. <b>Le <c>null</c> n'est pas une pièce vide</b> : « pas appelé » et
  /// « appelé, rien » sont deux déclarations différentes, et c'est la seconde qui a une valeur de
  /// preuve.
  /// </summary>
  /// <exception cref="ArgumentNullException"><paramref name="right"/> est absent.</exception>
  public Reading? ReadingIn(DataSubjectRight right, DeclaredSystemId declaredSystem)
  {
    ArgumentNullException.ThrowIfNull(right);

    return _readings.SingleOrDefault(
      reading => reading.Right == right && reading.DeclaredSystem == declaredSystem);
  }

  /// <summary>
  /// Un <c>Adapter</c> a <b>servi</b> un <c>Read</c>. Le dossier ne retient que le fait daté : la
  /// pièce, elle, est gardée <b>hors de l'agrégat</b>, pour que la remise l'efface sans le réécrire.
  /// </summary>
  /// <remarks>
  /// <b>Elle ne consigne rien.</b> La ligne de preuve est écrite par l'appelant, hors de l'agrégat —
  /// et c'est lui, et lui seul, qui décide de ne pas consigner un verdict identique au précédent.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="right"/> est absent.</exception>
  public Reading ReadServed(DataSubjectRight right, DeclaredSystemId declaredSystem, DateTimeOffset askedAt)
  {
    var reading = ReadingFor(right, declaredSystem, askedAt.ToUniversalTime());

    reading.Served(askedAt.ToUniversalTime(), _designations.Count);

    return reading;
  }

  /// <summary>
  /// Un <c>Adapter</c> a <b>différé</b> un <c>Read</c> et déclaré son échéance. Rien n'a été lu : le
  /// service repassera après elle, <b>à l'ouverture du dossier</b> et jamais depuis le tableau des
  /// demandes RGPD.
  /// </summary>
  /// <exception cref="ArgumentNullException"><paramref name="right"/> est absent.</exception>
  public Reading ReadDeferred(
    DataSubjectRight right,
    DeclaredSystemId declaredSystem,
    DateTimeOffset declaredDeadline,
    DateTimeOffset askedAt)
  {
    var reading = ReadingFor(right, declaredSystem, askedAt.ToUniversalTime());

    reading.Deferred(declaredDeadline.ToUniversalTime(), askedAt.ToUniversalTime(), _designations.Count);

    return reading;
  }

  /// <summary>
  /// Un <c>Adapter</c> a <b>refusé</b> un <c>Read</c>. Rien n'a été lu : un refus dit que le service
  /// et l'application ne sont pas d'accord, jamais ce que le système porte.
  /// </summary>
  /// <exception cref="ArgumentNullException"><paramref name="right"/> est absent.</exception>
  /// <exception cref="ArgumentException">La réponse donnée n'est pas un refus.</exception>
  public Reading ReadRefused(
    DataSubjectRight right,
    DeclaredSystemId declaredSystem,
    AdapterOutcome refusal,
    DateTimeOffset askedAt)
  {
    var reading = ReadingFor(right, declaredSystem, askedAt.ToUniversalTime());

    reading.Refused(refusal, askedAt.ToUniversalTime(), _designations.Count);

    return reading;
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
  /// signera au <c>EvidenceLog</c>.
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

    // Un dossier clos n'a plus aucune localisation : elles ont été détruites avec ce qu'elles
    // nommaient. La lecture le dit avant même de chercher, plutôt que de rendre null par accident.
    if (IsClosed)
    {
      return null;
    }

    var arbitrated = LocatingIn(declaredSystem)?.Arbitrate(reference, ruling);

    if (arbitrated is null || ruling != ReservationState.Attached)
    {
      return arbitrated;
    }

    foreach (var designation in arbitrated.Designations)
    {
      // Ce qui est déjà au sac n'y entre pas deux fois : le compte de l'EvidenceLog mesure l'ampleur d'une
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

    // Une question ouverte dit ce qui manque aujourd'hui à un dossier qu'on instruit. Un dossier clos
    // ne s'instruit plus, et les siennes viennent d'être détruites.
    if (IsClosed || _questions.Any(question => question.Subject == subject))
    {
      return false;
    }

    _questions.Add(new OpenQuestion(subject, askedOn.ToUniversalTime()));

    return true;
  }

  /// <summary>
  /// Retire une question à laquelle le dossier a fini par répondre, et dit si elle était posée.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Une question ouverte est du texte qui meurt : elle dit ce qui manque aujourd'hui.</b>
  /// La laisser après que la désignation a été trouvée en ferait un bandeau permanent, et un
  /// bandeau permanent s'apprend à ne plus se voir — la seule chose qu'un écran ne doive jamais
  /// enseigner. C'est la mécanique qui vaut déjà pour la réclamation d'une
  /// <c>IdentityMotivation</c>, et pour la même raison.
  /// </para>
  /// <para>
  /// <b>Rien n'est perdu de la preuve.</b> Le jour où la question s'est posée est au <c>EvidenceLog</c>,
  /// daté, et il y reste ; ce qui y a répondu — un <c>Locate</c> servi, une réserve rattachée —
  /// porte sa propre ligne datée. Le contrôle lit donc l'écart entre les deux sans qu'aucune ligne
  /// n'ait été réécrite, et l'écran, lui, ne montre que ce qui attend encore.
  /// </para>
  /// </remarks>
  /// <returns><c>true</c> si la question était posée ; <c>false</c> si elle ne l'était pas.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="subject"/> est absent.</exception>
  public bool Answered(OpenQuestionSubject subject)
  {
    ArgumentNullException.ThrowIfNull(subject);

    return _questions.RemoveAll(question => question.Subject == subject) > 0;
  }

  /// <summary>
  /// Un humain déclare que le service a <b>répondu</b> sur un droit, et dit si ce droit attendait
  /// encore une issue.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>C'est un geste à part de la clôture, et il le restera.</b> Clore un dossier ne peut pas
  /// valoir réponse sur six droits d'un seul clic : chaque droit est une réponse due à la personne,
  /// et la clôture n'en propage aucune. Ce que la clôture fait, c'est <b>réclamer</b> ces réponses —
  /// voir <see cref="ClaimsAwaitingAnOutcome"/> — sans jamais les exiger.
  /// </para>
  /// <para>
  /// <b>Elle n'affirme que l'acte de répondre.</b> Des <see cref="Step"/> restés inatteints ne la
  /// barrent pas, et restent lisibles un par un à côté d'elle.
  /// </para>
  /// <para>
  /// <b>Elle ne consigne rien.</b> La ligne de preuve est écrite par l'appelant, hors de l'agrégat.
  /// </para>
  /// </remarks>
  /// <param name="right">Le droit sur lequel le service déclare avoir répondu.</param>
  /// <returns>
  /// <c>true</c> si le droit vient de passer à <c>Answered</c> ; <c>false</c> si le dossier est
  /// clos, s'il ne porte pas ce droit, ou si une issue avait déjà été rendue.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="right"/> est absent.</exception>
  public bool Answer(DataSubjectRight right)
  {
    ArgumentNullException.ThrowIfNull(right);

    if (IsClosed)
    {
      return false;
    }

    return _claims.SingleOrDefault(one => one.Right == right)?.Answer() ?? false;
  }

  /// <summary>
  /// Un <c>Operator</c> déclare <b>prolonger de deux mois</b> au titre de l'art. 12.3 — un motif, et
  /// la date à laquelle il dit avoir informé la personne.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Le dossier enregistre, il ne prolonge pas.</b> Rien n'est écrit à la personne concernée : le
  /// service ne lui a jamais rien envoyé et ne commencera pas ici. L'échéance, elle, n'est pas
  /// touchée non plus — elle se <b>calcule</b> à l'affichage sur la date de cette déclaration.
  /// </para>
  /// <para>
  /// ⚠️ <b>Une déclaration tardive est acceptée telle quelle.</b> Elle s'inscrit au dossier et à la
  /// preuve, et le dénominateur ne bouge pas : le service consigne un fait laid plutôt qu'il ne
  /// fabrique un faux, et un dépassement déjà acquis reste lisible à côté.
  /// </para>
  /// <para>
  /// <b>Elle rend <c>false</c> plutôt qu'elle ne lève</b>, comme les autres gestes d'humain : sur un
  /// dossier clos, dont le délai est éteint, et sur un dossier déjà prolongé — l'art. 12.3 n'ouvre
  /// qu'une prolongation, et une seconde réécrirait le motif et les dates qu'un humain a signés.
  /// </para>
  /// </remarks>
  /// <param name="declaration">Ce que l'<c>Operator</c> déclare.</param>
  /// <returns><c>true</c> si la déclaration vient de se poser ; <c>false</c> si le dossier est clos ou déjà prolongé.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="declaration"/> est absent.</exception>
  public bool DeclareExtension(ExtensionDeclaration declaration)
  {
    ArgumentNullException.ThrowIfNull(declaration);

    if (IsClosed || ExtensionDeclaration is not null)
    {
      return false;
    }

    ExtensionDeclaration = declaration;

    return true;
  }

  /// <summary>
  /// Un humain <b>clôt le dossier</b>, et tout le nominatif est détruit <b>à l'instant même</b>, en
  /// une seule transaction.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Ce qui meurt raconte, ce qui survit se compte.</b> Tombent ici les
  /// <see cref="Designations"/> — la seule identité qui circule —, le <see cref="Locatings"/> avec
  /// les désignations et les motifs de réserve qu'il porte, les <see cref="Questions"/>, et le
  /// <b>détail</b> de la <see cref="Motivation"/>. Survivent la <c>Method</c> de cette motivation,
  /// qui se compte et dit sous quel régime le dossier a été instruit, les états déclarés des
  /// <see cref="Step"/>, et les dates.
  /// </para>
  /// <para>
  /// <b>Les <see cref="Readings"/> survivent, et ce n'est pas un oubli.</b> Une lecture ne retient
  /// qu'un droit, un système déclaré, une date et un nombre de pièces — rien qui nomme quiconque,
  /// les pièces elles-mêmes vivant dans les <c>RetrievedData</c> hors de l'agrégat. La vider
  /// n'effacerait aucun nominatif et perdrait le compte de ce que l'instruction a tenté, exactement
  /// comme le ferait remettre à zéro l'état d'un <see cref="Step"/>.
  /// </para>
  /// <para>
  /// <b>Aucune fenêtre de conservation, pas même « au cas où ».</b> Aucun risque juridique ne
  /// demande le nominatif : la preuve d'une procédure est anonyme, et le service ne prouve jamais
  /// qu'un droit a été honoré. Un délai de sûreté n'aurait entreposé que le sac de désignations de
  /// gens ayant demandé à disparaître.
  /// </para>
  /// <para>
  /// ⚠️ <b>Elle ne propage rien et ne gèle rien.</b> Aucun <see cref="Claim"/> ne passe à
  /// <c>Answered</c>, aucun <see cref="Step"/> ne change d'état : un <c>Step</c> laissé
  /// <see cref="StepState.ToDo"/> le reste, et se lit comme un oubli. L'incomplétude reste visible
  /// là où elle est vraie plutôt que masquée par un état de haut niveau rassurant.
  /// </para>
  /// <para>
  /// ⚠️ <b>Le geste est irréversible, seul du dispositif à l'être.</b> Sa parade est un geste
  /// délibéré dans la surface de l'<c>Operator</c>, jamais de la donnée gardée en réserve. La
  /// méthode se contente donc de rendre <c>false</c> sur un dossier déjà clos : reclore réécrirait
  /// la cause et la date qu'un humain a signées, sans rien rendre de ce qui est détruit.
  /// </para>
  /// <para>
  /// <b>Elle ne consigne rien, et ne touche pas au <c>EvidenceLog</c>.</b> La preuve est écrite par
  /// l'appelant et <b>survit au dossier de cinq ans</b> — elle continue de nommer l'<c>Operator</c>,
  /// dont l'effacement se refuse légitimement. Les <c>RetrievedData</c>, qui vivent hors de
  /// l'agrégat, sont détruites par l'appelant dans la même transaction.
  /// </para>
  /// </remarks>
  /// <param name="cause">Ce par quoi le dossier se clôt, nommé par l'humain qui signe.</param>
  /// <param name="closedOn">L'instant de la clôture, et de la destruction.</param>
  /// <returns><c>true</c> si le dossier vient de se clore ; <c>false</c> s'il l'était déjà.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="cause"/> est absent.</exception>
  public bool Close(ClosingCause cause, DateTimeOffset closedOn)
  {
    ArgumentNullException.ThrowIfNull(cause);

    if (IsClosed)
    {
      return false;
    }

    State = CaseState.Closed;
    ClosingCause = cause;
    ClosedOn = closedOn.ToUniversalTime();

    // Le sac de désignations : la seule identité qui circule, et la première à tomber.
    _designations.Clear();

    // Les localisations portent les désignations qu'une réserve proposait et le motif que
    // l'application en a écrit — du nominatif, quelle que soit l'opacité de leur forme. Ce que le
    // service a trouvé, et où, est déjà au EvidenceLog sous forme de comptes.
    _locatings.Clear();

    // Les questions ouvertes disent ce qui manquait aujourd'hui : du texte qui meurt, sans
    // lecteur demain. Le jour où chacune s'est posée reste daté au EvidenceLog.
    _questions.Clear();

    // La méthode survit, le détail meurt : le premier se compte et son lecteur est le contrôle, le
    // second nomme et n'a aucune raison de survivre à la personne dont il parle.
    Motivation = Motivation?.WithoutDetail();

    return true;
  }

  /// <summary>
  /// La localisation de ce système, posée si elle n'existait pas encore.
  /// </summary>
  /// <remarks>
  /// <b>Elle lève sur un dossier clos</b>, là où les gestes d'humain rendent faux. La différence
  /// n'est pas d'humeur : ces gestes-ci n'ont pas d'issue négative à rendre — ils rendent la
  /// localisation elle-même —, et surtout appeler un <c>Adapter</c> pour un dossier clos serait
  /// chercher la personne sous des désignations qui n'existent plus. C'est une faute de
  /// programmation de l'appelant, qui dispose de <see cref="IsClosed"/>, et non un cas de bord.
  /// </remarks>
  /// <exception cref="InvalidOperationException">Le dossier est clos.</exception>
  private Locating LocatingFor(DeclaredSystemId declaredSystem, DateTimeOffset askedAt)
  {
    RefuseWhenClosed();

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
  /// La lecture de ce système sous ce droit, posée si elle n'existait pas encore.
  /// </summary>
  /// <remarks>Elle lève sur un dossier clos, pour la raison dite en <see cref="LocatingFor"/>.</remarks>
  /// <exception cref="InvalidOperationException">Le dossier est clos.</exception>
  private Reading ReadingFor(DataSubjectRight right, DeclaredSystemId declaredSystem, DateTimeOffset askedAt)
  {
    ArgumentNullException.ThrowIfNull(right);

    RefuseWhenClosed();

    var known = ReadingIn(right, declaredSystem);

    if (known is not null)
    {
      return known;
    }

    var opened = new Reading(right, declaredSystem, askedAt, _designations.Count);

    _readings.Add(opened);

    return opened;
  }

  /// <summary>Refuse un appel d'<c>Adapter</c> sur un dossier dont le nominatif n'existe plus.</summary>
  /// <exception cref="InvalidOperationException">Le dossier est clos.</exception>
  private void RefuseWhenClosed()
  {
    if (IsClosed)
    {
      throw new InvalidOperationException(
        "Ce dossier est clos : ses désignations ont été détruites à l'instant de la clôture, et "
        + "aucun Locate ni Read ne peut plus chercher qui que ce soit.");
    }
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
