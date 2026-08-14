namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Ce qui désigne <b>une</b> colonne à l'intérieur d'un <see cref="Screening"/> : le <b>triplet</b>
/// schéma, table, colonne.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Un triplet, jamais le couple (table, colonne).</b> Le relevé porte son schéma, et un pivot
/// PostgreSQL couvrant deux schémas rendrait le couple ambigu dès la première base réelle — deux
/// <c>public.contact.email</c> et <c>archive.contact.email</c> se confondraient, et l'un des deux
/// arbitrages écraserait l'autre.
/// </para>
/// <para>
/// <b>La comparaison est ordinale, et le service ne replie aucune casse.</b> Les SGBD ne s'accordent
/// pas sur ce qu'ils replient — PostgreSQL abaisse ce qui n'est pas entre guillemets, MySQL dépend
/// de son système de fichiers. Choisir un repli ici reviendrait à trancher à la place du SGBD source
/// sur un fait que le relevé rapporte déjà tel qu'il est. <c>Enregistré, jamais vérifié</c> : on recopie.
/// </para>
/// </remarks>
public sealed record ColumnIdentity
{
  /// <summary>
  /// Le plafond d'un nom d'objet, en unités UTF-16. Vérifié et non supposé : PostgreSQL borne ses
  /// identifiants à 63 octets et MariaDB à 64, donc 100 les couvre tous avec de la marge.
  /// </summary>
  public const int MaxNameLength = 100;

  private ColumnIdentity(string schema, string table, string column)
  {
    Schema = schema;
    Table = table;
    Column = column;
  }

  /// <summary>Le schéma dont le relevé dit que la table vient.</summary>
  public string Schema { get; }

  /// <summary>La table, telle que le relevé la nomme.</summary>
  public string Table { get; }

  /// <summary>La colonne, telle que le relevé la nomme.</summary>
  public string Column { get; }

  /// <summary>
  /// Forge le triplet, ou refuse. Le refus est une <b>programmation fautive</b> : un relevé mal
  /// formé se refuse en bloc à l'ingestion, bien avant d'arriver ici.
  /// </summary>
  /// <exception cref="ArgumentException">Un des trois membres est vide, démesuré, ou porte un caractère de contrôle.</exception>
  public static ColumnIdentity Of(string? schema, string? table, string? column)
  {
    return new ColumnIdentity(
      ScreeningText.OrThrow(schema, "Le nom du schéma", MaxNameLength, nameof(schema)),
      ScreeningText.OrThrow(table, "Le nom de la table", MaxNameLength, nameof(table)),
      ScreeningText.OrThrow(column, "Le nom de la colonne", MaxNameLength, nameof(column)));
  }

  /// <summary>La table seule, ce qui est l'unité de travail de l'écran d'arbitrage.</summary>
  public TableIdentity TableIdentity => new(Schema, Table);

  /// <inheritdoc />
  public override string ToString()
  {
    return $"{Schema}.{Table}.{Column}";
  }
}

/// <summary>
/// La table dont une <see cref="ColumnIdentity"/> relève. Elle existe parce que l'<c>Operator</c>
/// arbitre <b>une table à la fois</b> : le commentaire de table éclaire toutes ses colonnes, et le
/// voisinage de <c>adr_l1</c>, <c>adr_l2</c>, <c>cp</c>, <c>ville</c> ne se lit pas colonne isolée.
/// </summary>
public sealed record TableIdentity(string Schema, string Table)
{
  /// <inheritdoc />
  public override string ToString()
  {
    return $"{Schema}.{Table}";
  }
}
