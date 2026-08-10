using System.Globalization;
using System.Text;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// De quoi coller un pivot lisible en trois lignes, et surtout de quoi en abîmer <b>une</b> à la
/// fois : chacun des neuf refus se prouve en partant d'un collage sincère et en lui retirant
/// exactement ce que le cas nomme.
/// </summary>
internal static class APivot
{
  internal const string Dialect = "postgresql";

  internal const string Database = "galette_prod";

  internal static readonly DateTimeOffset GeneratedOn = new(2026, 8, 10, 9, 30, 0, TimeSpan.Zero);

  /// <summary>La ligne d'en-tête d'un collage sincère.</summary>
  internal static string Header(
    string format = ColumnListing.FormatVersion,
    string dialect = Dialect,
    string database = Database)
  {
    return $$"""
      {"format":"{{format}}","dialecte":"{{dialect}}","base":"{{database}}","genere_le":"{{GeneratedOn.ToString("O", CultureInfo.InvariantCulture)}}"}
      """;
  }

  /// <summary>La ligne de fin, qui porte le compte que la requête a produit.</summary>
  internal static string Footer(int columnCount)
  {
    return $$"""{"fin":true,"colonnes":{{columnCount}}}""";
  }

  /// <summary>Une ligne de colonne, neuf champs, dans la forme que la requête émet.</summary>
  internal static string Column(
    string column,
    string table = "adherents",
    string schema = "public",
    int position = 1,
    string? dataType = "varchar(255)",
    bool? isNullable = true,
    string? columnComment = null,
    string? tableComment = null,
    string? referencedTable = null)
  {
    return new StringBuilder("{")
      .Append(Field("schema", schema)).Append(',')
      .Append(Field("table", table)).Append(',')
      .Append(Field("colonne", column)).Append(',')
      .Append($"\"position\":{position.ToString(CultureInfo.InvariantCulture)},")
      .Append(Field("type", dataType)).Append(',')
      .Append($"\"nullable\":{isNullable switch { true => "true", false => "false", null => "null" }},")
      .Append(Field("commentaire_colonne", columnComment)).Append(',')
      .Append(Field("commentaire_table", tableComment)).Append(',')
      .Append(Field("table_referencee", referencedTable))
      .Append('}')
      .ToString();
  }

  /// <summary>Un collage entier : l'en-tête, les lignes qu'on lui donne, et la ligne de fin qui les compte.</summary>
  internal static string Paste(params string[] columns)
  {
    return Paste(columns.Length, columns);
  }

  /// <summary>
  /// Un collage dont on choisit <b>séparément</b> le compte annoncé et les lignes rendues : c'est le
  /// seul moyen de fabriquer la troncature au milieu que la ligne de fin existe pour attraper.
  /// </summary>
  internal static string Paste(int declaredColumnCount, params string[] columns)
  {
    return string.Join('\n', [Header(), .. columns, Footer(declaredColumnCount)]);
  }

  /// <summary>Le collage sincère de référence : deux tables, quatre colonnes, dans l'ordre du schéma.</summary>
  internal static string ASincerePaste()
  {
    return Paste(
      Column("id_adh", position: 1, tableComment: "les adhérents"),
      Column("adr_l1", position: 2, tableComment: "les adhérents"),
      Column("id_cotis", table: "cotisations", position: 1, referencedTable: "adherents"),
      Column("dt_naiss", position: 3, dataType: "date", tableComment: "les adhérents"));
  }

  private static string Field(string key, string? value)
  {
    return value is null
      ? $"\"{key}\":null"
      : $"\"{key}\":\"{value}\"";
  }
}
