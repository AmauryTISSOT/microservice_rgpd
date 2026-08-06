namespace MicroserviceRgpd.Infrastructure.Data.Casework;

/// <summary>
/// La ligne du <c>Ledger</c> telle qu'elle est écrite : la projection à plat de
/// <see cref="Core.Casework.Ledger.LedgerEntry"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle est confinée à l'infrastructure, et n'est jamais déclarée agrégat racine.</b> Le dépôt
/// générique est contraint à <c>IAggregateRoot</c> ; marquer cette classe l'aurait fait s'appliquer
/// à elle, et aurait déclaré agrégat ce qui est <b>hors de l'agrégat</b> par construction — le
/// <c>Ledger</c> survit au <c>Case</c> de cinq ans, et un dépôt lui aurait rendu la mise à jour et
/// la suppression ligne à ligne que sa définition ferme.
/// </para>
/// <para>
/// <b>Aucune colonne n'accepte une <c>Designation</c> ni un nom de personne concernée</b>, dès la
/// première ligne. Les colonnes sont écrites une par une plutôt que dérivées : ce qui n'a pas de
/// colonne ne s'écrira pas, et l'anonymat tient par la forme de la table plutôt que par la
/// discipline de qui l'alimente.
/// </para>
/// <para>
/// <b>Aucune règle ne vit ici.</b> Les invariants sont portés par les types du domaine ; cette
/// classe n'existe que pour qu'EF Core ait des colonnes à remplir.
/// </para>
/// </remarks>
public sealed class LedgerRow
{
  /// <summary>L'identité de la ligne. Jamais un rang, jamais un compteur.</summary>
  public required Guid EntryId { get; init; }

  /// <summary>Le dossier dont cette ligne est la preuve — et à qui elle survivra de cinq ans.</summary>
  public required Guid CaseId { get; init; }

  /// <summary>L'instant du fait, en UTC.</summary>
  public required DateTimeOffset OccurredAt { get; init; }

  /// <summary>Ce que cette ligne consigne, par son nom, dans un vocabulaire fermé.</summary>
  public required string Fact { get; init; }

  /// <summary>Un <c>Operator</c> nommé, ou l'application du client — c'est-à-dire personne.</summary>
  public required string SignatoryKind { get; init; }

  /// <summary>
  /// Le nom de l'<c>Operator</c>, ou <c>null</c> quand l'application a appelé. <b>C'est le seul
  /// nom de personne que cette table porte</b>, et ce n'est jamais celui de la personne concernée.
  /// </summary>
  public string? SignatoryName { get; init; }

  /// <summary>
  /// La déclaration d'identité en vigueur, quand le fait en dépend. Une <b>pratique</b> que le
  /// contrôle dénombre, jamais une donnée sur la personne.
  /// </summary>
  public string? IdentityDeclaration { get; init; }

  /// <summary>
  /// Le <b>nombre</b> de désignations sous lesquelles la personne est cherchée — jamais lesquelles.
  /// C'est une mesure de l'ampleur d'une recherche ; les valeurs seraient le sac lui-même, qui
  /// meurt à la clôture.
  /// </summary>
  public int? DesignationCount { get; init; }

  /// <summary>
  /// Le <c>DeclaredSystem</c> que le fait concerne, quand il en concerne un. C'est un nom du
  /// <b>paysage déclaré du client</b> — choisi par l'humain qui l'a recensé — et jamais un nom de
  /// personne concernée.
  /// </summary>
  public string? DeclaredSystem { get; init; }

  /// <summary>
  /// Sous quel régime l'<c>Operator</c> a saisi son nom, ou <c>null</c> quand l'application a
  /// appelé. Il s'écrit <b>en même temps</b> que le nom : sans lui, la ligne d'aujourd'hui serait
  /// indiscernable de celle de demain.
  /// </summary>
  public string? SignatureRegime { get; init; }

  /// <summary>
  /// Le droit au titre duquel le fait a eu lieu, quand il en concerne un. Un mot de la taxonomie du
  /// RGPD, jamais une donnée sur la personne.
  /// </summary>
  public string? DataSubjectRight { get; init; }

  /// <summary>L'état déclaré du travail dû, quand le fait en déclare un — <c>Untreated</c> compris.</summary>
  public string? StepState { get; init; }

  /// <summary>
  /// La <b>prose de preuve</b> : le constat, le motif. C'est la seule prose que cette table porte, et
  /// la prose de <em>travail</em> n'y a <b>aucune colonne</b> — elle vit sur le <c>Case</c> et meurt à
  /// la clôture.
  /// </summary>
  public string? EvidenceProse { get; init; }

  /// <summary>
  /// La date de réception du dossier était-elle tenue pour défaut ? Ce que le service a <b>supposé</b>,
  /// jamais ce que quelqu'un a déclaré.
  /// </summary>
  public bool? ReceptionWasDefaulted { get; init; }

  /// <summary>
  /// Le jour depuis lequel le mois de l'art. 12.3 se compte, tel que le canal l'a dit. Distincte de
  /// <see cref="OccurredAt"/>, qui date le <b>geste</b> : un courriel transcrit d'une boîte aux
  /// lettres a été reçu avant d'être déposé, et une seule colonne aurait fait choisir entre dater le
  /// geste et dater le délai.
  /// </summary>
  public DateTimeOffset? ReceivedOn { get; init; }

  /// <summary>
  /// La <b>moitié qui se compte</b> de la motivation d'identité — c'est elle qui survit à la
  /// clôture. ⚠️ Le détail en prose n'a <b>aucune colonne ici</b> : il est nominatif par nature, il
  /// vit sur le <c>Case</c> et meurt avec lui. Le contrôle juge ainsi la pratique sans qu'un seul nom
  /// lui survive.
  /// </summary>
  public string? IdentityVerificationMethod { get; init; }

  /// <summary>
  /// L'échéance qu'un <c>Adapter</c> a <b>déclarée</b> en différant. Elle est de lui, jamais du
  /// service : trois dates disent tout d'un travail différé — appelé, échéance déclarée, résultat —
  /// et cette table n'en portera jamais une quatrième qui compterait les relances.
  /// </summary>
  public DateTimeOffset? DeclaredDeadline { get; init; }

  /// <summary>
  /// Combien de <c>DeclaredSystem</c> recensés la réponse remise <b>couvrait</b>. Le numérateur du
  /// « 2 sur 6 » que le contrôle vient lire, et qui ne descend <b>jamais</b> dans la
  /// <c>CoverSheet</c> : écrit à la personne, il affirmerait que le client a exactement six systèmes.
  /// </summary>
  public int? CoveredSystemCount { get; init; }

  /// <summary>
  /// Combien de <c>DeclaredSystem</c> le catalogue recensait au moment de la remise — le
  /// dénominateur du même « 2 sur 6 ». Il est <b>écrit</b> plutôt que relu plus tard : le recensement
  /// vieillit exprès, et le relire dans trois ans jugerait la pratique d'hier au paysage de demain.
  /// </summary>
  public int? DeclaredSystemCount { get; init; }
}
