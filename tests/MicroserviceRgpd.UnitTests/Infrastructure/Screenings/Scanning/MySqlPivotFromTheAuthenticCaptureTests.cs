using System.Reflection;
using System.Text;
using System.Text.Json;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning.MySql;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings.Scanning;

/// <summary>
/// <b>Le pivot que le C# écrit rend-il le même relevé que celui que la requête servie a réellement
/// produit ?</b> C'est la seconde moitié du filet du dialecte MariaDB/MySQL — celle que le lint sur
/// le texte des requêtes ne couvre pas — et elle ne demande aucun conteneur.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ce que le test rejoue, et ce qu'il ne rejoue pas.</b> Les captures de
/// <c>exploration/releves-authentiques/captures/</c> sont les octets réellement sortis de MySQL 8.4
/// et de MariaDB 11.8. Chaque ligne de colonne y porte, champ par champ, ce que
/// l'<c>information_schema</c> de ces serveurs a rendu : c'est de <b>là</b> que le test refait les
/// lignes du catalogue, et il les passe ensuite à la traduction du dialecte connecté, puis au
/// <see cref="PivotWriter"/>, puis à l'ingestion. Ce qui est éprouvé est donc la <b>couture sans
/// serveur</b> : ligne de catalogue → pivot → relevé accepté. Ce qui ne l'est pas — que la requête
/// du dialecte rende bien ces lignes-là — reste au lint sur son texte, et au jour où un test à
/// conteneurs sera reconsidéré.
/// </para>
/// <para>
/// ⚠️ <b>Les deux moteurs sont là parce que c'est entre eux que le type change de nom.</b> La
/// colonne <c>etiquettes</c> est déclarée <c>JSON</c> dans la fixture ; MySQL la rend <c>json</c>,
/// MariaDB la rend <c>longtext</c> — elle n'y est qu'un alias de <c>LONGTEXT</c>. Un service dont
/// la liste noire dépendrait de ce nom rendrait deux relevés différents pour la <b>même</b> base
/// selon le serveur qui la sert.
/// </para>
/// </remarks>
public class MySqlPivotFromTheAuthenticCaptureTests
{
  private static readonly DateTimeOffset Midi =
    new(2026, 8, 25, 13, 38, 32, TimeSpan.Zero);

  /// <summary>Les deux captures de la famille, et le nom que chacune donne au conteneur libre.</summary>
  public static TheoryData<string, string> Captures =>
    new()
    {
      { "releve-mysql84.txt", "json" },
      { "releve-mariadb118.txt", "longtext" },
    };

  /// <summary>
  /// <b>Le pivot écrit par le service est accepté par l'ingestion, et rend les mêmes colonnes que la
  /// capture authentique.</b>
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>« Les mêmes colonnes », et non « à peu près les mêmes ».</b> Les
  /// <see cref="ListedColumn"/> se comparent entières — identité, position, type, nullabilité, les
  /// deux commentaires et la table référencée. Comparer les seuls noms laisserait passer le défaut
  /// qui compte : un <c>nullable</c> constant, un commentaire perdu, un <c>varchar</c> écrit là où
  /// le chemin collé écrit <c>varchar(255)</c>.
  /// </remarks>
  [Theory]
  [MemberData(nameof(Captures))]
  public void WritesAPivotTheIngestionAcceptsAndThatMatchesTheAuthenticOne(
    string capture,
    string freeContainer)
  {
    var authentic = ColumnListingIngestion.Ingest(Capture(capture));

    authentic.Refusal.ShouldBeNull();

    var written = ColumnListingIngestion.Ingest(
      PivotWriter.Write(
        DatabaseDialect.MySql,
        authentic.Listing!.Database,
        Midi,
        MySqlCatalogue.InPivotOrder(CatalogueBehind(capture))));

    written.Refusal.ShouldBeNull(
      "Le pivot que le dialecte connecté écrit est refusé par l'ingestion, alors que celui de la "
      + "requête servie passe. Les deux chemins doivent rendre le même format — c'est ce qui fait "
      + "de la voie connectée une entrée de plus, et non un second chemin dans le domaine.");

    written.Listing!.Dialect.ShouldBe(authentic.Listing.Dialect);
    written.Listing.Database.ShouldBe(authentic.Listing.Database);

    written.Listing.Columns
      .OrderBy(column => column.Identity.ToString(), StringComparer.Ordinal)
      .ShouldBe(
        authentic.Listing.Columns.OrderBy(
          column => column.Identity.ToString(),
          StringComparer.Ordinal),
        "Le relevé connecté et le relevé collé de la même base ne rendent pas les mêmes colonnes.");

    // La capture porte bien le nom que ce moteur-là donne au conteneur libre, sans quoi le test ne
    // dirait rien de la divergence qu'il est censé couvrir.
    written.Listing.Columns.ShouldContain(
      column => column.Identity.Column == "etiquettes" && column.DataType == freeContainer);
  }

  /// <summary>
  /// ⚠️ <b>La nullabilité arrive en texte et repart en booléen, sur les deux moteurs.</b> Un
  /// <c>"YES"</c> écrit tel quel serait refusé ligne à ligne ; un <c>nullable</c> constant, lui,
  /// passerait — et désactiverait le filtre de nullabilité sur toute la base sans un mot.
  /// </summary>
  [Theory]
  [MemberData(nameof(Captures))]
  public void CarriesBothNullabilitiesAsRealBooleans(string capture, string freeContainer)
  {
    _ = freeContainer;

    var columns = MySqlCatalogue.InPivotOrder(CatalogueBehind(capture));

    columns.ShouldContain(column => column.IsNullable == true);
    columns.ShouldContain(column => column.IsNullable == false);
  }

  /// <summary>
  /// ⚠️ <b>Le conteneur libre se prélève, et les octets ne se prélèvent pas — quel que soit le
  /// moteur.</b> La fixture porte les deux : <c>etiquettes</c>, qui change de nom d'un serveur à
  /// l'autre, et <c>photo</c>/<c>signature</c>, qui n'en changent pas.
  /// </summary>
  [Theory]
  [MemberData(nameof(Captures))]
  public void SamplesTheSameColumnsOnBothEngines(string capture, string freeContainer)
  {
    _ = freeContainer;

    var refused = CatalogueBehind(capture)
      .Where(column => !MySqlScanQueries.IsSampleable(column.DataType))
      .Select(column => column.Scanned.Identity.Column)
      .Order(StringComparer.Ordinal)
      .ToList();

    refused.ShouldBe(
      ["photo", "signature"],
      Case.Sensitive,
      "Les colonnes écartées du prélèvement ne sont pas les mêmes d'un moteur à l'autre. Le "
      + "conteneur libre (`json` sur MySQL, `longtext` sur MariaDB) se prélève des deux côtés ; "
      + "seuls les octets s'écartent.");
  }

  /// <summary>
  /// Le catalogue tel que le serveur l'a rendu, refait depuis la capture : une ligne de
  /// <c>information_schema</c> par ligne de colonne du pivot.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>La famille du type se déduit du type complet, et c'est exact.</b>
  /// <c>DATA_TYPE</c> est <c>COLUMN_TYPE</c> amputé de sa longueur et de ses attributs :
  /// <c>varchar(255)</c> donne <c>varchar</c>, <c>decimal(10,2)</c> donne <c>decimal</c>,
  /// <c>tinyint(1)</c> donne <c>tinyint</c>. Le pivot ne porte que le second — c'est ce que la
  /// requête servie écrit —, et la couper ici est ce qui permet de relire une capture comme un
  /// catalogue.
  /// </remarks>
  private static List<MySqlCataloguedColumn> CatalogueBehind(string capture)
  {
    var columns = new List<MySqlCataloguedColumn>();

    foreach (var line in Capture(capture).Split('\n', StringSplitOptions.RemoveEmptyEntries))
    {
      using var document = JsonDocument.Parse(line);
      var fields = document.RootElement;

      if (!fields.TryGetProperty("colonne", out _))
      {
        // L'en-tête et la ligne de fin ne sont pas des colonnes.
        continue;
      }

      var columnType = fields.GetProperty("type").GetString() ?? string.Empty;

      columns.Add(
        MySqlCatalogue.ToColumn(
          fields.GetProperty("schema").GetString()!,
          fields.GetProperty("table").GetString()!,
          fields.GetProperty("colonne").GetString()!,
          fields.GetProperty("position").GetInt32(),
          columnType,
          FamilyOf(columnType),
          fields.GetProperty("nullable").GetBoolean() ? MySqlCatalogue.Nullable : "NO",
          fields.GetProperty("commentaire_colonne").GetString()!,
          fields.GetProperty("commentaire_table").GetString()!,
          fields.GetProperty("table_referencee").GetString()!));
    }

    return columns;
  }

  private static string FamilyOf(string columnType)
  {
    var attributes = columnType.IndexOfAny([' ', '(']);

    return attributes < 0 ? columnType : columnType[..attributes];
  }

  /// <summary>
  /// Une capture authentique, telle qu'elle est versionnée. Voir
  /// <c>exploration/releves-authentiques/README.md</c>.
  /// </summary>
  private static string Capture(string name)
  {
    using var stream = typeof(MySqlPivotFromTheAuthenticCaptureTests).GetTypeInfo().Assembly
      .GetManifestResourceStream(name)
      ?? throw new InvalidOperationException(
        $"La capture « {name} » n'est pas embarquée dans les tests. Sans elle, rien ne met le pivot "
        + "que le service écrit face à celui que la requête servie a réellement produit.");

    using var reader = new StreamReader(stream, Encoding.UTF8);

    return reader.ReadToEnd();
  }
}
