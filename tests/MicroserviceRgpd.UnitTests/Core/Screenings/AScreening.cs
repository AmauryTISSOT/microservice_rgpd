using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// De quoi poser un rapport lisible en trois lignes. Les noms de colonnes sont ceux qu'on croise
/// vraiment dans un schéma français — <c>adr_l1</c>, <c>dt_naiss</c> — parce qu'un test qui
/// s'appelle <c>col1</c> ne dit plus si le domaine tient devant du réel.
/// </summary>
internal static class AScreening
{
  internal static readonly DateTimeOffset LaunchedOn = new(2026, 8, 6, 9, 30, 0, TimeSpan.Zero);

  internal static readonly ScreeningEngineIdentity Engine = new("lexique-fr-en", "1.0.0");

  /// <summary>Une ligne du relevé, nommée par son triplet.</summary>
  internal static ListedColumn AListedColumn(
    string column,
    string table = "adherents",
    string schema = "public",
    int position = 1,
    string? dataType = "varchar(255)",
    string? columnComment = null,
    string? tableComment = null)
  {
    return ListedColumn.Of(
      ColumnIdentity.Of(schema, table, column),
      position,
      dataType,
      isNullable: true,
      columnComment,
      tableComment);
  }

  /// <summary>Un rapport portant exactement les lignes qu'on lui donne.</summary>
  internal static Screening Of(params ScreenedColumn[] columns)
  {
    return Screening.Of(
      ScreeningId.Next(),
      "galette_prod",
      "postgresql",
      Engine,
      columns.Length,
      columns,
      LaunchedOn);
  }

  /// <summary>Un rapport lancé à un instant donné : c'est le seul fait dont dépend « courant ».</summary>
  internal static Screening LaunchedAt(DateTimeOffset launchedOn, ScreeningId? id = null)
  {
    return Screening.Of(
      id ?? ScreeningId.Next(),
      "galette_prod",
      "postgresql",
      Engine,
      declaredColumnCount: 1,
      [ScreenedColumn.NothingSeen(AListedColumn("id_adh"))],
      launchedOn);
  }

  /// <summary>La ligne signalée type : un préfixe reconnu, un degré qui dit quelle règle, un motif en prose.</summary>
  internal static ScreenedColumn AFlaggedColumn(
    string column = "adr_l1",
    int position = 1,
    string table = "adherents")
  {
    return ScreenedColumn.Flagged(
      AListedColumn(column, table: table, position: position),
      PersonalDataCategory.ContactDetails,
      RuleStrength.Morphological,
      $"préfixe « adr » reconnu dans « {column} »");
  }
}
