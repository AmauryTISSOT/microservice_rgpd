using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Screenings.Scanning;

/// <summary>
/// Une colonne telle que le catalogue de la base l'a rendue, <b>avant</b> qu'elle ne devienne une
/// ligne du format pivot. C'est le seul objet que les dialectes se partagent : chacun sait lire son
/// propre catalogue, aucun ne sait écrire le pivot.
/// </summary>
/// <remarks>
/// ⚠️ <b>Ce n'est pas un <see cref="ListedColumn"/>, et il ne doit pas le devenir.</b> Un
/// <see cref="ListedColumn"/> n'existe qu'au bout de <see cref="ColumnListingIngestion.Ingest"/> :
/// le fabriquer ici court-circuiterait les neuf refus et ferait de la voie connectée un second
/// chemin d'entrée dans le domaine.
/// </remarks>
internal sealed record ScannedColumn(
  ColumnIdentity Identity,
  int Position,
  string? DataType,
  bool? IsNullable,
  string? ReferencedTable)
{
  /// <summary>
  /// Le commentaire porté par la colonne, quand le SGBD en connaît. SQLite n'en rend jamais aucun ;
  /// PostgreSQL et MySQL en rendront, et c'est pourquoi le champ existe déjà.
  /// </summary>
  public string? ColumnComment { get; init; }

  /// <summary>Le commentaire porté par la table, aux mêmes conditions.</summary>
  public string? TableComment { get; init; }

  /// <summary>
  /// Le nom de schéma <b>tel que le catalogue l'a rendu</b>, avant que le domaine ne le normalise.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>C'est celui-ci, et jamais celui de l'<see cref="ColumnIdentity"/>, qui entre dans une
  /// requête.</b> Le domaine rogne les blancs de bordure — il a raison : deux colonnes qui ne
  /// diffèrent que par une espace de tête ne sont pas deux colonnes pour un lecteur humain. Mais un
  /// SGBD, lui, accepte parfaitement une table nommée <c>" abonne "</c>, et la requête qui
  /// demanderait <c>"abonne"</c> tomberait sur « table inconnue » — donc sur une raison d'absence
  /// pour toutes ses colonnes, alors que le schéma avait été lu sans le moindre problème.
  /// </remarks>
  public string RawSchema { get; init; } = string.Empty;

  /// <summary>Le nom de table tel que le catalogue l'a rendu. Voir <see cref="RawSchema"/>.</summary>
  public string RawTable { get; init; } = string.Empty;

  /// <summary>Le nom de colonne tel que le catalogue l'a rendu. Voir <see cref="RawSchema"/>.</summary>
  public string RawColumn { get; init; } = string.Empty;
}
