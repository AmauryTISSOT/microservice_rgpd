using System.Globalization;
using System.Text;
using System.Text.Json;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Screenings.Scanning;

/// <summary>
/// Écrit le texte <c>screening-pivot/1</c> à partir des colonnes brutes qu'un dialecte a relevées :
/// une ligne d'en-tête, une ligne par colonne, une ligne de clôture.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le compte annoncé est calculé sur ce qui est écrit, et il ne peut pas s'en écarter.</b>
/// C'est la <b>divergence de glossaire</b> du chemin connecté : sur le chemin collé, « il est entier
/// ou il n'existe pas » tient <b>par un mécanisme</b> — la requête déclare son propre compte, et un
/// collage tronqué se fait prendre par <c>CountMismatch</c>. À la connexion, il n'y a <b>aucun
/// équivalent</b> : c'est le service qui produit ce compte. Il le tient donc auto-cohérent en
/// l'écrivant depuis la liste qu'il vient de sérialiser, jamais depuis un compte reçu à part.
/// Autrement dit : <c>CountMismatch</c> ne peut plus rien attraper sur cette voie, et ce n'est pas
/// une régression — c'est la même clause tenue un cran plus haut.
/// </para>
/// <para>
/// ⚠️ <b>Un relevé au compte partiel est sincère, partiel et muet.</b> Si la base n'a rendu qu'une
/// partie de son catalogue sans le dire, le pivot écrit ici sera parfaitement valide et le
/// <c>Screening</c> parfaitement cohérent : rien, dans le format, ne pourra signaler le manque. La
/// clause d'incomplétude du chemin connecté ne se lit donc pas dans le texte pivot — c'est le
/// dialecte, plus bas, qui doit ne jamais rendre un catalogue amputé en silence.
/// </para>
/// <para>
/// ⚠️ <b>Les neuf clés de la ligne de colonne sont toujours écrites.</b>
/// <see cref="ColumnListingIngestion"/> exige la <b>présence</b> de chaque clé, avec son type ; une
/// clé omise parce que sa valeur est absente ferait refuser la ligne en
/// <c>UnreadableColumnLine</c>. Une valeur absente s'écrit <c>null</c>, jamais rien.
/// </para>
/// </remarks>
internal static class PivotWriter
{
  /// <summary>
  /// L'instant s'écrit en UTC suffixé d'un <c>Z</c>, comme <c>releves/*.sql</c> l'écrit déjà.
  /// </summary>
  /// <remarks>
  /// ⚠️ Un décalage écrit <c>+00:00</c> traverserait <c>System.Text.Json</c> échappé en
  /// <c>+</c> : le pivot resterait valide et l'ingestion le relirait sans broncher, mais un
  /// relevé connecté et un relevé collé de la même base ne se ressembleraient plus à l'œil — et
  /// c'est à l'œil que quelqu'un les compare le jour où quelque chose cloche.
  /// </remarks>
  private const string InstantFormat = "yyyy-MM-ddTHH:mm:ss'Z'";

  internal static string Write(
    DatabaseDialect dialect,
    string database,
    DateTimeOffset generatedOn,
    IReadOnlyList<ScannedColumn> columns)
  {
    var text = new StringBuilder();

    text.Append('{')
      .Append("\"format\":").Append(Quote(ColumnListing.FormatVersion)).Append(',')
      .Append("\"dialecte\":").Append(Quote(dialect.PivotName)).Append(',')
      .Append("\"base\":").Append(Quote(database)).Append(',')
      .Append("\"genere_le\":")
      .Append(Quote(generatedOn.UtcDateTime.ToString(InstantFormat, CultureInfo.InvariantCulture)))
      .Append('}')
      .Append('\n');

    foreach (var column in columns)
    {
      text.Append('{')
        .Append("\"schema\":").Append(Quote(column.Identity.Schema)).Append(',')
        .Append("\"table\":").Append(Quote(column.Identity.Table)).Append(',')
        .Append("\"colonne\":").Append(Quote(column.Identity.Column)).Append(',')
        .Append("\"position\":")
        .Append(column.Position.ToString(CultureInfo.InvariantCulture))
        .Append(',')
        .Append("\"type\":").Append(Quote(column.DataType)).Append(',')
        .Append("\"nullable\":").Append(Boolean(column.IsNullable)).Append(',')
        .Append("\"commentaire_colonne\":").Append(Quote(column.ColumnComment)).Append(',')
        .Append("\"commentaire_table\":").Append(Quote(column.TableComment)).Append(',')
        .Append("\"table_referencee\":").Append(Quote(column.ReferencedTable))
        .Append('}')
        .Append('\n');
    }

    text.Append('{')
      .Append("\"fin\":true,")
      .Append("\"colonnes\":")
      .Append(columns.Count.ToString(CultureInfo.InvariantCulture))
      .Append('}')
      .Append('\n');

    return text.ToString();
  }

  private static string Quote(string? value)
  {
    return value is null ? "null" : JsonSerializer.Serialize(value);
  }

  private static string Boolean(bool? value)
  {
    return value switch
    {
      true => "true",
      false => "false",
      null => "null",
    };
  }
}
