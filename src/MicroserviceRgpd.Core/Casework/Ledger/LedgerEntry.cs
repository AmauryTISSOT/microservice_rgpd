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
    bool? receptionWasDefaulted = null)
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

  /// <summary>L'instant du fait, en UTC.</summary>
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
  /// La première ligne d'un dossier : il s'est ouvert, à telle date, sous telle déclaration
  /// d'identité, avec tant de désignations pour chercher la personne.
  /// </summary>
  /// <param name="caseId">Le dossier qui vient de s'ouvrir.</param>
  /// <param name="occurredAt">L'instant de l'ouverture.</param>
  /// <param name="signatory">Qui a fait entrer la demande.</param>
  /// <param name="identityDeclaration">Ce que le canal d'entrée a déclaré de l'identité du demandeur.</param>
  /// <param name="designationCount">Le nombre de désignations reçues — jamais lesquelles.</param>
  /// <param name="receptionWasDefaulted">
  /// La date de réception a-t-elle été <b>tenue pour défaut</b> faute de déclaration ? Le défaut
  /// s'inscrit <b>comme un défaut</b> : sans lui, la preuve garderait la même trace d'une date
  /// affirmée par un humain et d'une hypothèse du service.
  /// </param>
  /// <exception cref="ArgumentNullException">Un argument obligatoire est absent.</exception>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="designationCount"/> est négatif.</exception>
  public static LedgerEntry CaseOpened(
    CaseId caseId,
    DateTimeOffset occurredAt,
    Signatory signatory,
    IdentityDeclaration identityDeclaration,
    int designationCount,
    bool receptionWasDefaulted)
  {
    ArgumentNullException.ThrowIfNull(signatory);
    ArgumentNullException.ThrowIfNull(identityDeclaration);
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
      receptionWasDefaulted: receptionWasDefaulted);
  }

  /// <summary>
  /// Un <c>Operator</c> a <b>déclaré</b> où en est le travail dû sur un système, et il a écrit son
  /// constat.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Le constat est exigé.</b> Un état déclaré sans un mot serait une preuve qui dit ce qui a été
  /// coché et non ce qui a été constaté — or c'est précisément le constat que le contrôle vient lire,
  /// et le seul rempart contre six zéros qui se liraient « cette personne n'est pas chez nous ».
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
  /// <param name="evidenceProse">Le constat de l'<c>Operator</c> — prose de preuve, qui survit.</param>
  /// <param name="signatory">L'humain qui signe, et le régime sous lequel il a saisi son nom.</param>
  /// <exception cref="ArgumentNullException">Un argument obligatoire est absent.</exception>
  /// <exception cref="ArgumentException">
  /// Le constat est absent, vide, démesuré, ou porte un caractère de contrôle ; ou la ligne n'est
  /// signée par aucun humain.
  /// </exception>
  public static LedgerEntry StepDeclared(
    CaseId caseId,
    DateTimeOffset occurredAt,
    DeclaredSystemId declaredSystem,
    DataSubjectRight right,
    StepState state,
    string? evidenceProse,
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
      DeclaredText.OrThrow(evidenceProse, "Le constat", MaxEvidenceProseLength, nameof(evidenceProse)));
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
