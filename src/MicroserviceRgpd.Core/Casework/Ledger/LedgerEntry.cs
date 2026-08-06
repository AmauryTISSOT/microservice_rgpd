using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Core.Casework.Ledger;

/// <summary>
/// Une ligne de la matière de preuve d'un <see cref="Case"/> : qui a déclaré quoi, et quand.
/// </summary>
/// <remarks>
/// <para>
/// <b>Anonyme par construction, jamais par expurgation.</b> Aucune <see cref="Designation"/>,
/// aucun nom de personne concernée n'entre ici — <b>dès la première ligne</b>, et non à la
/// clôture. Anonymiser plus tard aurait exigé de réécrire le <c>Ledger</c> : la seule structure du
/// dispositif dont l'invariant est précisément qu'on ne la réécrit pas. La règle tient par la
/// <b>forme du type</b> — il n'existe aucun emplacement où une désignation pourrait atterrir — et
/// non par la discipline de qui l'écrit.
/// </para>
/// <para>
/// <b>Il n'est anonyme que côté personne concernée.</b> Il nomme l'<c>Operator</c>, définitivement.
/// </para>
/// <para>
/// <b>Il ne porte jamais de contenu.</b> Il dit « un fichier a été remis le 12/04 couvrant 2
/// systèmes sur 6 », jamais ce qu'il y avait dedans. Les nombres qu'il porte sont des mesures
/// destinées au contrôle, qui juge une pratique.
/// </para>
/// <para>
/// <b>Rien de son écriture ne dépend d'une ligne antérieure</b> : ni rang, ni total courant, ni
/// chaîne d'empreintes. C'est ce qui rend l'ajout seul vérifiable plutôt que promis — une écriture
/// qui lirait la ligne d'avant serait une écriture qu'une ligne d'avant pourrait faire mentir.
/// </para>
/// </remarks>
public sealed record LedgerEntry
{
  private LedgerEntry(
    LedgerEntryId id,
    CaseId caseId,
    DateTimeOffset occurredAt,
    LedgerFact fact,
    Signatory signatory,
    IdentityDeclaration? identityDeclaration,
    int? designationCount,
    DeclaredSystemId? declaredSystem,
    DataSubjectRight? right = null,
    StepState? declaredState = null,
    string? evidenceProse = null,
    bool? receptionWasDefaulted = null,
    IdentityVerificationMethod? verificationMethod = null,
    DateTimeOffset? receivedOn = null,
    DateTimeOffset? declaredDeadline = null)
  {
    Id = id;
    Case = caseId;
    OccurredAt = occurredAt;
    Fact = fact;
    Signatory = signatory;
    IdentityDeclaration = identityDeclaration;
    DesignationCount = designationCount;
    DeclaredSystem = declaredSystem;
    Right = right;
    DeclaredState = declaredState;
    EvidenceProse = evidenceProse;
    ReceptionWasDefaulted = receptionWasDefaulted;
    VerificationMethod = verificationMethod;
    ReceivedOn = receivedOn;
    DeclaredDeadline = declaredDeadline;
  }

  /// <summary>
  /// Le plafond de la prose de preuve, en unités UTF-16. Un constat, un motif — pas un dossier
  /// entier recopié dans la preuve.
  /// </summary>
  public const int MaxEvidenceProseLength = 2000;

  /// <summary>L'identité de cette ligne. Jamais un rang, jamais un compteur.</summary>
  public LedgerEntryId Id { get; }

  /// <summary>
  /// Le dossier dont cette ligne est la preuve. <b>Il lui survit</b> : le <c>Case</c> est détruit
  /// de son nominatif à la clôture, le <c>Ledger</c> vit cinq ans de plus.
  /// </summary>
  public CaseId Case { get; }

  /// <summary>
  /// L'instant du fait, en UTC — <b>l'instant du geste</b>, jamais celui dont le geste parle.
  /// </summary>
  /// <remarks>
  /// ⚠️ Sur une ouverture, c'est l'instant du <b>dépôt</b> et non la date de réception : le dépôt
  /// manuel transcrit un courriel reçu il y a trois semaines, et dater la ligne d'il y a trois
  /// semaines ferait dire à la preuve que le service savait depuis trois semaines. Ce qu'il a su et
  /// quand est précisément ce que le <c>Ledger</c> est là pour établir. La date de réception, elle,
  /// a sa colonne propre — voir <see cref="ReceivedOn"/>.
  /// </remarks>
  public DateTimeOffset OccurredAt { get; }

  /// <summary>Ce que cette ligne consigne, dans un vocabulaire fermé.</summary>
  public LedgerFact Fact { get; }

  /// <summary>Qui l'a déclaré — un <c>Operator</c> nommé, ou l'application, c'est-à-dire personne.</summary>
  public Signatory Signatory { get; }

  /// <summary>
  /// Ce que le canal a déclaré de l'identité du demandeur, quand le fait en dépend. C'est une
  /// <b>pratique</b> que le contrôle dénombre, jamais une donnée sur la personne : la valeur dit ce
  /// que le service a exigé, pas qui s'est présenté.
  /// </summary>
  public IdentityDeclaration? IdentityDeclaration { get; }

  /// <summary>
  /// Le <b>nombre</b> de <see cref="Designation"/> sous lesquelles la personne est cherchée, quand
  /// le fait en dépend — jamais leurs valeurs. « Recherché sous 2 désignations » est une mesure de
  /// l'ampleur d'une recherche ; les deux valeurs seraient le sac lui-même, qui meurt à la clôture.
  /// </summary>
  public int? DesignationCount { get; }

  /// <summary>
  /// Le <see cref="Casework.DeclaredSystem"/> que le fait concerne, quand il en concerne un — jamais
  /// une personne : c'est un nom du paysage déclaré du client, choisi par l'humain qui l'a recensé.
  /// <c>null</c> pour les faits qui portent sur le dossier entier.
  /// </summary>
  public DeclaredSystemId? DeclaredSystem { get; }

  /// <summary>
  /// Le droit au titre duquel le fait a eu lieu, quand il en concerne un. C'est un mot de la
  /// taxonomie du RGPD, jamais une donnée sur la personne.
  /// </summary>
  public DataSubjectRight? Right { get; }

  /// <summary>
  /// L'état déclaré du travail dû, quand le fait en déclare un. <c>Untreated</c> y entre aussi
  /// volontiers que <c>Done</c> : c'est l'aveu que personne ne l'a fait, et il est la trace la plus
  /// précieuse du dispositif.
  /// </summary>
  public StepState? DeclaredState { get; }

  /// <summary>
  /// La <b>prose de preuve</b> — le constat, le motif — écrite par l'<c>Operator</c> au point de
  /// décision, et <c>null</c> pour les faits qu'aucun humain n'a motivés.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>C'est le seul champ de prose libre du <c>Ledger</c>, et son régime est écrit ici.</b> La
  /// prose de preuve dit <i>pourquoi on a décidé cela</i>, n'est pas nominative par nature, et
  /// survit. La <b>prose de travail</b> — celle qui dit quelle ligne appartient à qui, et qui nomme
  /// des tiers — n'a <b>aucun emplacement ici</b> : elle vit sur le <c>Case</c> et meurt à la
  /// clôture. La règle tient par le <b>placement</b> — deux champs à deux endroits, dont un seul
  /// survit — et non par la discipline de l'<c>Operator</c>.
  /// </para>
  /// <para>
  /// Elle est bornée et nettoyée comme tout texte déclaré : elle descend dans une colonne, et le
  /// service ne l'échappera pour personne.
  /// </para>
  /// </remarks>
  public string? EvidenceProse { get; }

  /// <summary>
  /// La date de réception du dossier était-elle <b>tenue pour défaut</b> ? Renseignée à l'ouverture,
  /// et <c>null</c> partout ailleurs.
  /// <para>
  /// C'est ce que le service a <b>supposé</b>, non ce que quelqu'un a déclaré — et c'est exactement
  /// pourquoi la preuve le garde : un défaut consigné comme un fait déclaré ferait relire dans dix
  /// ans une hypothèse du service comme l'affirmation d'un humain.
  /// </para>
  /// </summary>
  public bool? ReceptionWasDefaulted { get; }

  /// <summary>
  /// Le jour depuis lequel le mois de l'art. 12.3 se compte, tel que le canal l'a dit. Renseignée à
  /// l'ouverture, et <c>null</c> partout ailleurs.
  /// </summary>
  /// <remarks>
  /// <b>Elle ne se confond pas avec <see cref="OccurredAt"/>, et c'est le dépôt manuel qui l'exige.</b>
  /// Une demande transcrite d'une boîte aux lettres a été reçue avant d'être déposée, parfois de
  /// beaucoup : une seule date aurait fait choisir entre dater le geste et dater le délai, et le
  /// contrôle a besoin des deux — l'une dit depuis quand la personne attend, l'autre depuis quand le
  /// service savait. Elle se lit <b>toujours avec <see cref="ReceptionWasDefaulted"/></b>, qui dit si
  /// quelqu'un l'a affirmée ou si le service l'a supposée.
  /// </remarks>
  public DateTimeOffset? ReceivedOn { get; }

  /// <summary>
  /// <b>La moitié qui se compte</b> de ce que l'humain a pesé avant d'ouvrir un droit sous une
  /// identité qui ne repose sur aucun contrôle du canal. Renseignée à l'ouverture quand quelqu'un
  /// l'a pesé, et <c>null</c> partout ailleurs.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Le détail en prose n'entre jamais ici, et il n'existe aucune colonne où il pourrait
  /// atterrir.</b> Il dit <i>qui</i> a été rappelé et <i>sur quoi</i> — il est nominatif par nature,
  /// il vit sur le <see cref="Case"/> et meurt à sa clôture. Le contrôle juge ainsi la <b>pratique</b>
  /// sans qu'un seul nom lui survive : « douze accès ouverts sous <c>None</c> » est une mesure, le
  /// détail des douze serait le dossier.
  /// </para>
  /// <para>
  /// ⚠️ <b><c>null</c> et <see cref="IdentityVerificationMethod.None"/> ne se confondent pas.</b>
  /// <c>None</c> est ce que quelqu'un a déclaré ; le <c>null</c> est le fait que personne ne l'ait
  /// pesé. C'est exactement la distinction que <see cref="ReceptionWasDefaulted"/> tient pour la
  /// date, et pour la même raison : une hypothèse du service ne doit jamais se relire comme
  /// l'affirmation d'un humain.
  /// </para>
  /// </remarks>
  public IdentityVerificationMethod? VerificationMethod { get; }

  /// <summary>
  /// L'échéance qu'un <c>Adapter</c> a <b>déclarée</b> en différant, et <c>null</c> partout ailleurs.
  /// </summary>
  /// <remarks>
  /// <b>Elle est déclarée par le client, jamais négociée ni inventée</b> — et c'est pourquoi elle
  /// entre dans la preuve : trois dates disent tout d'un travail différé — appelé, échéance déclarée,
  /// résultat — là où un compteur de relances ne dirait que combien de fois un affichage a rappelé
  /// quelque chose à quelqu'un.
  /// </remarks>
  public DateTimeOffset? DeclaredDeadline { get; }

  /// <summary>
  /// La première ligne d'un dossier : il s'est ouvert, à telle date, sous telle déclaration
  /// d'identité, avec tant de désignations pour chercher la personne.
  /// </summary>
  /// <param name="caseId">Le dossier qui vient de s'ouvrir.</param>
  /// <param name="occurredAt">
  /// L'instant du <b>dépôt</b> — celui où la demande est entrée dans le service, jamais celui où le
  /// responsable de traitement l'a reçue.
  /// </param>
  /// <param name="signatory">Qui a fait entrer la demande.</param>
  /// <param name="identityDeclaration">Ce que le canal d'entrée a déclaré de l'identité du demandeur.</param>
  /// <param name="designationCount">Le nombre de désignations reçues — jamais lesquelles.</param>
  /// <param name="reception">
  /// La date de réception <b>et son régime</b>, pris ensemble : c'est le régime que la preuve garde —
  /// un défaut s'inscrit <b>comme un défaut</b>, sans quoi elle garderait la même trace d'une date
  /// affirmée par un humain et d'une hypothèse du service. La paire ne se sépare pas en chemin.
  /// </param>
  /// <param name="verificationMethod">
  /// La <b>moitié qui se compte</b> de la motivation, ou <c>null</c> si personne ne l'a pesée. Le
  /// détail en prose reste sur le <see cref="Case"/> et n'a aucun chemin vers ici.
  /// </param>
  /// <exception cref="ArgumentNullException">Un argument obligatoire est absent.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="designationCount"/> est négatif.</exception>
  public static LedgerEntry CaseOpened(
    CaseId caseId,
    DateTimeOffset occurredAt,
    Signatory signatory,
    IdentityDeclaration identityDeclaration,
    int designationCount,
    ReceptionDate reception,
    IdentityVerificationMethod? verificationMethod = null)
  {
    ArgumentNullException.ThrowIfNull(signatory);
    ArgumentNullException.ThrowIfNull(identityDeclaration);
    ArgumentNullException.ThrowIfNull(reception);
    ArgumentOutOfRangeException.ThrowIfNegative(designationCount);

    return new LedgerEntry(
      LedgerEntryId.Next(),
      caseId,
      // L'instant est ramené en UTC : `timestamptz` ne conserve pas le décalage, et laisser passer
      // une heure locale ferait dépendre la preuve du fuseau de la machine qui l'a écrite.
      occurredAt.ToUniversalTime(),
      LedgerFact.CaseOpened,
      signatory,
      identityDeclaration,
      designationCount,
      declaredSystem: null,
      receptionWasDefaulted: reception.IsDefault,
      verificationMethod: verificationMethod,
      receivedOn: reception.On);
  }

  /// <summary>
  /// Un <c>Operator</c> a <b>repris à son compte</b> un droit qu'une <c>Qualification</c> avait
  /// seulement proposé.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Le signataire est un humain nommé, et il ne peut pas être l'application.</b> Confirmer est
  /// précisément le geste qui fait passer une proposition de machine au compte de quelqu'un : le
  /// laisser signer par l'application viderait la ligne de tout son sens, et la preuve dirait qu'un
  /// droit a été confirmé par personne.
  /// </para>
  /// <para>
  /// <b>Aucune prose n'est réclamée.</b> Confirmer, c'est dire « oui, ce droit-là » : le fait, le
  /// droit, la date et le nom disent tout. Exiger un constat ferait écrire une ligne de rien à chaque
  /// confirmation, et le constat qui compte se noierait dans les autres.
  /// </para>
  /// </remarks>
  /// <param name="caseId">Le dossier dans lequel la confirmation a eu lieu.</param>
  /// <param name="occurredAt">L'instant du geste.</param>
  /// <param name="right">Le droit repris à son compte.</param>
  /// <param name="signatory">L'humain qui signe, et le régime sous lequel il a saisi son nom.</param>
  /// <exception cref="ArgumentNullException">Un argument obligatoire est absent.</exception>
  /// <exception cref="ArgumentException">La ligne n'est signée par aucun humain.</exception>
  public static LedgerEntry ClaimConfirmed(
    CaseId caseId,
    DateTimeOffset occurredAt,
    DataSubjectRight right,
    Signatory signatory)
  {
    ArgumentNullException.ThrowIfNull(right);
    ArgumentNullException.ThrowIfNull(signatory);

    if (signatory.Kind != SignatoryKind.Operator)
    {
      throw new ArgumentException(
        "Une confirmation est le geste d'un Operator nommé : l'application ne confirme rien, et "
        + "c'est tout ce qui distingue un droit reconnu par quelqu'un d'un droit proposé par une machine.",
        nameof(signatory));
    }

    return new LedgerEntry(
      LedgerEntryId.Next(),
      caseId,
      occurredAt.ToUniversalTime(),
      LedgerFact.ClaimConfirmed,
      signatory,
      identityDeclaration: null,
      designationCount: null,
      declaredSystem: null,
      right);
  }

  /// <summary>
  /// Un <c>Operator</c> a écrit <b>après coup</b> ce qu'il avait pesé de l'identité du demandeur.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Elle ne remplace pas la ligne d'ouverture, elle s'ajoute à elle.</b> Le <c>Ledger</c> est en
  /// ajout seul : la déclaration d'aujourd'hui ne réécrit pas la preuve d'hier, et l'<b>écart</b>
  /// entre les deux dates est précisément ce que le contrôle doit pouvoir voir — un accès ouvert
  /// lundi sur la foi de rien, pesé vendredi, n'est pas un accès pesé avant d'être ouvert.
  /// </para>
  /// <para>
  /// ⚠️ <b>La méthode seule, comme à l'ouverture.</b> Le détail en prose nomme par nature, reste sur
  /// le <see cref="Case"/> et meurt avec lui : il n'a aucune colonne ici.
  /// </para>
  /// </remarks>
  /// <param name="caseId">Le dossier dont l'identité a été pesée.</param>
  /// <param name="occurredAt">L'instant où quelqu'un l'a écrite.</param>
  /// <param name="verificationMethod">La moitié qui se compte, et la seule qui survive.</param>
  /// <param name="signatory">L'humain qui signe, et le régime sous lequel il a saisi son nom.</param>
  /// <exception cref="ArgumentNullException">Un argument obligatoire est absent.</exception>
  /// <exception cref="ArgumentException">La ligne n'est signée par aucun humain.</exception>
  public static LedgerEntry MotivationDeclared(
    CaseId caseId,
    DateTimeOffset occurredAt,
    IdentityVerificationMethod verificationMethod,
    Signatory signatory)
  {
    ArgumentNullException.ThrowIfNull(verificationMethod);
    ArgumentNullException.ThrowIfNull(signatory);

    if (signatory.Kind != SignatoryKind.Operator)
    {
      throw new ArgumentException(
        "Une motivation est pesée par un Operator nommé : l'application ne pèse rien, et c'est "
        + "précisément ce qu'on lui demande d'avoir fait.",
        nameof(signatory));
    }

    return new LedgerEntry(
      LedgerEntryId.Next(),
      caseId,
      occurredAt.ToUniversalTime(),
      LedgerFact.MotivationDeclared,
      signatory,
      identityDeclaration: null,
      designationCount: null,
      declaredSystem: null,
      verificationMethod: verificationMethod);
  }

  /// <summary>
  /// Un <c>Operator</c> a <b>déclaré</b> où en est le travail dû sur un système, et il a écrit son
  /// constat.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Le constat est exigé là où l'état le réclame</b> — c'est-à-dire sur un <c>Done</c> <b>à zéro
  /// rattachement</b>, et par la même règle que celle dont l'écran se sert pour le réclamer : voir
  /// <see cref="Case.FindingIsDemandedBy"/>. Un « fait » sans un mot serait une preuve qui dit ce
  /// qui a été coché et non ce qui a été constaté, et c'est le seul rempart contre six zéros qui se
  /// liraient « cette personne n'est pas chez nous ».
  /// </para>
  /// <para>
  /// <b>Ailleurs, le constat est accueilli sans être exigé.</b> Exiger une prose sur chaque état ferait
  /// écrire une ligne de rien à chaque clic, et le constat qui compte se noierait dans les autres —
  /// mais rien n'empêche d'en écrire un, et il entre dans la preuve comme les autres.
  /// </para>
  /// <para>
  /// <b>Aucune désignation, ni même leur compte.</b> Ce fait ne mesure aucune recherche : il dit
  /// l'état d'un travail dû, et un compte se lirait comme une ampleur qu'il n'a pas.
  /// </para>
  /// </remarks>
  /// <param name="caseId">Le dossier dont ce travail était dû.</param>
  /// <param name="occurredAt">L'instant de la déclaration.</param>
  /// <param name="declaredSystem">Le système sur lequel le travail était dû.</param>
  /// <param name="right">Le droit au titre duquel il l'était.</param>
  /// <param name="state">L'état déclaré, <c>Untreated</c> compris.</param>
  /// <param name="evidenceProse">
  /// Le constat de l'<c>Operator</c> — prose de preuve, qui survit. Exigé lorsque l'état le réclame,
  /// accueilli sinon.
  /// </param>
  /// <param name="findingIsDemanded">
  /// Le dossier réclame-t-il un constat pour cette déclaration ? La règle vit sur le
  /// <see cref="Case"/>, qui seul détient les deux moitiés — l'état déclaré et les rattachements du
  /// système. La redire ici en ferait une seconde règle, qui finirait par ne plus dire la même chose
  /// que l'écran.
  /// </param>
  /// <param name="signatory">L'humain qui signe, et le régime sous lequel il a saisi son nom.</param>
  /// <exception cref="ArgumentNullException">Un argument obligatoire est absent.</exception>
  /// <exception cref="ArgumentException">
  /// Le constat est démesuré, porte un caractère de contrôle, ou manque là où l'état le réclame ; ou
  /// la ligne n'est signée par aucun humain.
  /// </exception>
  public static LedgerEntry StepDeclared(
    CaseId caseId,
    DateTimeOffset occurredAt,
    DeclaredSystemId declaredSystem,
    DataSubjectRight right,
    StepState state,
    string? evidenceProse,
    bool findingIsDemanded,
    Signatory signatory)
  {
    ArgumentNullException.ThrowIfNull(right);
    ArgumentNullException.ThrowIfNull(state);
    ArgumentNullException.ThrowIfNull(signatory);

    if (signatory.Kind != SignatoryKind.Operator)
    {
      // Un constat est le geste d'un humain, par définition : c'est lui qui a regardé. Le laisser
      // signer par l'application ferait porter à personne une déclaration que quelqu'un a faite.
      throw new ArgumentException(
        "Un constat est déclaré par un Operator nommé : l'application ne constate rien.",
        nameof(signatory));
    }

    return new LedgerEntry(
      LedgerEntryId.Next(),
      caseId,
      occurredAt.ToUniversalTime(),
      LedgerFact.StepDeclared,
      signatory,
      identityDeclaration: null,
      designationCount: null,
      declaredSystem,
      right,
      state,
      FindingOrThrow(evidenceProse, findingIsDemanded));
  }

  /// <summary>
  /// Le constat, nettoyé — <b>exigé là où le dossier le réclame</b>, accueilli ailleurs, et
  /// <c>null</c> quand il n'y en a pas et qu'aucun n'était réclamé.
  /// </summary>
  /// <remarks>
  /// <b>Elle est publique pour que la frontière de saisie puisse nommer le refus à l'humain</b> sous le
  /// nom de son champ, sans avoir à fabriquer une signature pour éprouver sa prose. Ce qui reste écrit
  /// <b>ici</b>, une seule fois, est la <b>forme</b> du constat ; <b>qui</b> le réclame est une règle
  /// du <see cref="Case"/> — voir <see cref="Case.FindingIsDemandedBy"/> —, parce qu'elle a besoin
  /// des rattachements que seul le dossier détient.
  /// </remarks>
  /// <param name="evidenceProse">Ce que l'humain a écrit, ou rien.</param>
  /// <param name="findingIsDemanded">Le dossier réclame-t-il un constat pour cette déclaration ?</param>
  /// <exception cref="ArgumentException">
  /// Le constat est démesuré, porte un caractère de contrôle, ou manque là où il est réclamé.
  /// </exception>
  public static string? FindingOrThrow(string? evidenceProse, bool findingIsDemanded)
  {
    if (findingIsDemanded || !string.IsNullOrWhiteSpace(evidenceProse))
    {
      return DeclaredText.OrThrow(evidenceProse, "Le constat", MaxEvidenceProseLength, nameof(evidenceProse));
    }

    // Rien à consigner, et rien n'était réclamé : la colonne reste vide plutôt que de porter une
    // chaîne vide, qui se lirait comme un constat qu'on aurait effacé.
    return null;
  }

  /// <summary>
  /// Un <c>Adapter</c> a refusé un appel : la <b>tentative datée</b>, et rien d'autre. Le dossier,
  /// lui, n'a pas bougé — aucun <c>Step</c> n'a changé d'état, et cette ligne ne prétend pas le
  /// contraire.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Le signataire est l'application, c'est-à-dire personne</b> : un appel sortant n'est le
  /// geste d'aucun humain nommé, et lui donner une signature d'<c>Operator</c> ferait porter à
  /// quelqu'un un refus qu'il n'a pas prononcé.
  /// </para>
  /// <para>
  /// <b>Aucune désignation, ni même leur compte.</b> Un refus n'a rien cherché : il n'a pas
  /// d'ampleur à mesurer, et un zéro se lirait comme une recherche menée sous rien.
  /// </para>
  /// </remarks>
  /// <param name="caseId">Le dossier au titre duquel l'appel est parti.</param>
  /// <param name="occurredAt">L'instant de la tentative.</param>
  /// <param name="declaredSystem">Le système sur lequel on demandait à exercer.</param>
  /// <param name="refusal">Lequel des deux refus l'<c>Adapter</c> a rendu.</param>
  /// <exception cref="ArgumentNullException"><paramref name="refusal"/> est absent.</exception>
  /// <exception cref="ArgumentException">La réponse donnée n'est pas un refus.</exception>
  public static LedgerEntry AdapterRefused(
    CaseId caseId,
    DateTimeOffset occurredAt,
    DeclaredSystemId declaredSystem,
    AdapterOutcome refusal)
  {
    return new LedgerEntry(
      LedgerEntryId.Next(),
      caseId,
      occurredAt.ToUniversalTime(),
      FactOf(AdapterOutcome.RefusalOrThrow(refusal, nameof(refusal))),
      Signatory.Application,
      identityDeclaration: null,
      designationCount: null,
      declaredSystem);
  }

  /// <summary>
  /// Un <c>Adapter</c> a <b>servi</b> un <c>Locate</c> : la tentative datée, le système, et
  /// <b>sous combien de désignations</b> on a cherché.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Le compte est la seule mesure, et il porte sur la recherche — jamais sur ce qu'on a
  /// trouvé.</b> « Recherché sous 2 désignations » dit l'ampleur de ce que le service a tenté, ce que
  /// le contrôle vient juger ; dénombrer les rattachements ferait entrer dans la preuve une mesure des
  /// données de la personne, que le <c>Ledger</c> ne porte jamais. Aucune valeur de désignation
  /// n'entre ici, et il n'existe aucune colonne où elle pourrait atterrir.
  /// </para>
  /// <para>
  /// <b>Le signataire est l'application, c'est-à-dire personne</b> : un appel sortant n'est le geste
  /// d'aucun humain nommé.
  /// </para>
  /// </remarks>
  /// <param name="caseId">Le dossier au titre duquel l'appel est parti.</param>
  /// <param name="occurredAt">L'instant de la tentative.</param>
  /// <param name="declaredSystem">Le système où l'on a cherché.</param>
  /// <param name="designationCount">Le nombre de désignations portées par l'appel — jamais lesquelles.</param>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="designationCount"/> est négatif.</exception>
  public static LedgerEntry LocateServed(
    CaseId caseId,
    DateTimeOffset occurredAt,
    DeclaredSystemId declaredSystem,
    int designationCount)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(designationCount);

    return new LedgerEntry(
      LedgerEntryId.Next(),
      caseId,
      occurredAt.ToUniversalTime(),
      LedgerFact.LocateServed,
      Signatory.Application,
      identityDeclaration: null,
      designationCount,
      declaredSystem);
  }

  /// <summary>
  /// Un <c>Adapter</c> a <b>différé</b> un <c>Locate</c> et déclaré son échéance.
  /// </summary>
  /// <remarks>
  /// <b>L'échéance entre dans la preuve parce qu'elle est déclarée par le client</b>, et parce que
  /// trois dates disent tout de ce travail : appelé, échéance déclarée, résultat. Ce qu'on ne saura
  /// jamais, en revanche, est combien de fois le service est repassé — une relance n'a aucun
  /// signataire, et son compte serait du bruit de mécanique dans ce que le contrôle vient lire.
  /// </remarks>
  /// <param name="caseId">Le dossier au titre duquel l'appel est parti.</param>
  /// <param name="occurredAt">L'instant de la tentative.</param>
  /// <param name="declaredSystem">Le système où l'on cherchait.</param>
  /// <param name="declaredDeadline">L'échéance que l'<c>Adapter</c> a déclarée.</param>
  /// <param name="designationCount">Le nombre de désignations portées par l'appel — jamais lesquelles.</param>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="designationCount"/> est négatif.</exception>
  public static LedgerEntry LocateDeferred(
    CaseId caseId,
    DateTimeOffset occurredAt,
    DeclaredSystemId declaredSystem,
    DateTimeOffset declaredDeadline,
    int designationCount)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(designationCount);

    return new LedgerEntry(
      LedgerEntryId.Next(),
      caseId,
      occurredAt.ToUniversalTime(),
      LedgerFact.LocateDeferred,
      Signatory.Application,
      identityDeclaration: null,
      designationCount,
      declaredSystem,
      declaredDeadline: declaredDeadline.ToUniversalTime());
  }

  /// <summary>
  /// Un <c>Operator</c> a <b>tranché une réserve</b> de <c>Locate</c> : il a dit que cette ligne
  /// était celle de la personne, ou qu'elle ne l'était pas.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Le signataire est un humain nommé, et il ne peut pas être l'application.</b> C'est très
  /// exactement ce que l'arbitrage est : ni la fusion à tort — irréversible, et portant sur la donnée
  /// d'un tiers — ni l'exclusion par prudence n'appartiennent à une machine.
  /// </para>
  /// <para>
  /// ⚠️ <b>Ni la référence, ni le motif, ni les désignations n'entrent ici.</b> Ce sont de la prose de
  /// travail et du nominatif : ils vivent sur le <see cref="Case"/> et meurent à sa clôture. Ce qui
  /// survit est le <b>fait daté</b> et le <b>compte du sac après l'arbitrage</b> — de quoi lire
  /// « recherché sous 2 désignations, dont 1 ajoutée par arbitrage le 12/04 » sans qu'aucune valeur
  /// n'ait été gardée.
  /// </para>
  /// </remarks>
  /// <param name="caseId">Le dossier dans lequel l'arbitrage a eu lieu.</param>
  /// <param name="occurredAt">L'instant du geste.</param>
  /// <param name="declaredSystem">Le système où la réserve avait été levée.</param>
  /// <param name="ruling">L'issue rendue : rattachée, ou écartée.</param>
  /// <param name="designationCount">Le compte du sac <b>après</b> l'arbitrage — jamais son contenu.</param>
  /// <param name="signatory">L'humain qui signe, et le régime sous lequel il a saisi son nom.</param>
  /// <exception cref="ArgumentNullException">Un argument obligatoire est absent.</exception>
  /// <exception cref="ArgumentException">
  /// L'issue n'est pas un arbitrage, ou la ligne n'est signée par aucun humain.
  /// </exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="designationCount"/> est négatif.</exception>
  public static LedgerEntry ReservationArbitrated(
    CaseId caseId,
    DateTimeOffset occurredAt,
    DeclaredSystemId declaredSystem,
    ReservationState ruling,
    int designationCount,
    Signatory signatory)
  {
    ArgumentNullException.ThrowIfNull(signatory);
    ArgumentOutOfRangeException.ThrowIfNegative(designationCount);

    if (signatory.Kind != SignatoryKind.Operator)
    {
      throw new ArgumentException(
        "Une réserve est tranchée par un Operator nommé : le service ne décide aucun rattachement, "
        + "ni par fusion à tort, ni par exclusion par prudence.",
        nameof(signatory));
    }

    return new LedgerEntry(
      LedgerEntryId.Next(),
      caseId,
      occurredAt.ToUniversalTime(),
      ReservationState.RulingOrThrow(ruling, nameof(ruling)) == ReservationState.Attached
        ? LedgerFact.ReservationAttached
        : LedgerFact.ReservationSetAside,
      signatory,
      identityDeclaration: null,
      designationCount,
      declaredSystem);
  }

  /// <summary>
  /// Une <c>OpenQuestion</c> est née sur le dossier : <b>tous</b> les <c>Locate</c> ont rendu zéro, et
  /// la désignation ne suffit donc pas.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Le signataire est l'application</b>, et c'est exact : personne n'a rien décidé — le service a
  /// constaté que ses propres appels ne rattachaient rien. La question est ce constat, daté ; elle
  /// <b>n'arrête pas</b> le délai de l'art. 12.3, et rien de cette ligne ne prétend le contraire.
  /// </para>
  /// <para>
  /// <b>Le compte du sac l'accompagne</b> : « rien trouvé sous 1 désignation » et « rien trouvé sous
  /// 4 » ne se jugent pas pareil, et c'est précisément la question que la ligne pose.
  /// </para>
  /// <para>
  /// ⚠️ <b>Aucun sujet n'est écrit, et ce n'est pas un oubli.</b> Une question du dossier ne porte
  /// aucun droit ; celle du contenu d'un droit, quand <c>Read</c> sera exercé, portera le sien dans
  /// <see cref="Right"/> — la colonne existe déjà, et c'est elle qui distinguera les deux sans
  /// qu'un vocabulaire de plus entre dans la preuve.
  /// </para>
  /// </remarks>
  /// <param name="caseId">Le dossier sur lequel la question naît.</param>
  /// <param name="occurredAt">L'instant du constat, qui est aussi la date de la question.</param>
  /// <param name="designationCount">Le nombre de désignations sous lesquelles on a cherché.</param>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="designationCount"/> est négatif.</exception>
  public static LedgerEntry QuestionRaised(CaseId caseId, DateTimeOffset occurredAt, int designationCount)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(designationCount);

    return new LedgerEntry(
      LedgerEntryId.Next(),
      caseId,
      occurredAt.ToUniversalTime(),
      LedgerFact.QuestionRaised,
      Signatory.Application,
      identityDeclaration: null,
      designationCount,
      declaredSystem: null);
  }

  /// <summary>
  /// Le fait que consigne un refus. <b>Les deux refus gardent leur distinction jusque dans la
  /// preuve</b> : ils ne se réparent pas au même endroit, et un « appel refusé » unique ferait
  /// chercher au mauvais endroit qui relira.
  /// </summary>
  private static LedgerFact FactOf(AdapterOutcome refusal)
  {
    // Le refus est déjà garanti par l'appelant ; ce qui reste est la seule correspondance du
    // dispositif entre ce que le transport a répondu et ce que la preuve en garde.
    return refusal == AdapterOutcome.SecretRefused
      ? LedgerFact.AdapterRefusedTheSecret
      : LedgerFact.AdapterDidNotServeTheSystem;
  }
}
