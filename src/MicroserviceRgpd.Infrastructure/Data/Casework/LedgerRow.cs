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
}
