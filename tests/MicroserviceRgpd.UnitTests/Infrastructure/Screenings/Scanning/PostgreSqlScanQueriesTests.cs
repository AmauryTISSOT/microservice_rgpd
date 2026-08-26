using System.Globalization;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning.PostgreSql;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings.Scanning;

/// <summary>
/// Le <b>lint sur le texte</b> des requêtes PostgreSQL embarquées : la moitié du filet de sécurité
/// qui ne demande aucun conteneur. Les faits par dialecte ont été mesurés sur conteneur hors du
/// dépôt (#275) ; ici, il n'y a que du texte à relire — et le texte, lui, se lit toujours.
/// </summary>
/// <remarks>
/// ⚠️ <b>Ni cette moitié ni l'autre ne suffit.</b> Le lint ne voit pas si la requête rend le bon
/// relevé — c'est le rejeu de la fixture, un fichier plus loin, qui s'en charge —, et le rejeu ne
/// voit pas ce qui coûterait cher sur dix millions de lignes. C'est pour cela qu'elles sont deux.
/// </remarks>
public class PostgreSqlScanQueriesTests
{
  /// <summary>
  /// Tout ce que le scanner PostgreSQL envoie : les requêtes fixes, et une requête de prélèvement
  /// telle qu'elle est réellement construite.
  /// </summary>
  public static TheoryData<string, string> EveryQuery =>
    new()
    {
      { nameof(PostgreSqlScanQueries.DatabasePresence), PostgreSqlScanQueries.DatabasePresence },
      { nameof(PostgreSqlScanQueries.Catalogue), PostgreSqlScanQueries.Catalogue },
      { nameof(PostgreSqlScanQueries.Sample), ASampleQuery },
    };

  private static string ASampleQuery =>
    PostgreSqlScanQueries.Sample("public", "adherents", ["courriel", "meta", "adr_l1"]);

  /// <summary>
  /// ⚠️ <b>Le relevé lit <c>pg_catalog</c>, et rien de l'<c>information_schema</c>.</b> Les vues
  /// conformes sont filtrées ligne à ligne par privilège : un compte sans droit sur une table ne la
  /// voit pas, et le relevé serait <b>silencieusement partiel</b> — sincère, entier de son point de
  /// vue, amputé de la moitié des tables du client, sans que rien nulle part ne sache qu'il manque
  /// quelque chose.
  /// </summary>
  [Theory]
  [MemberData(nameof(EveryQuery))]
  public void NoQueryReadsTheInformationSchema(string name, string query)
  {
    query.ShouldNotContain(
      "information_schema.",
      Case.Insensitive,
      $"La requête {name} lit une vue de l'information_schema, filtrée par privilège.");
  }

  /// <summary>
  /// Le pendant positif du test précédent : le catalogue est bien lu <b>quelque part</b>, et un
  /// filtre nommé n'est pas une lecture.
  /// </summary>
  [Fact]
  public void TheCatalogueQueryReadsPgCatalog()
  {
    PostgreSqlScanQueries.Catalogue.ShouldContain("pg_catalog.pg_attribute");
    PostgreSqlScanQueries.Catalogue.ShouldContain("pg_catalog.pg_class");
    PostgreSqlScanQueries.Catalogue.ShouldContain("pg_catalog.pg_namespace");
  }

  /// <summary>
  /// ⚠️ <b>Pas d'étoile.</b> <c>SELECT *</c> prélèverait les colonnes gagnées depuis le relevé —
  /// donc des colonnes absentes du rapport, échantillonnées quand même.
  /// </summary>
  [Theory]
  [MemberData(nameof(EveryQuery))]
  public void NoQueryCarriesAStar(string name, string query)
  {
    query.ShouldNotContain("*", Case.Sensitive, $"La requête {name} porte une étoile.");
  }

  /// <summary>
  /// ⚠️ <b>Pas d'<c>ORDER BY</c>, pas même en fenêtre.</b> Le biais du premier venu est énoncé, pas
  /// corrigé : trier coûterait un parcours complet de la table du client pour cinq valeurs. La
  /// renumérotation des positions, que la requête collée confie à un <c>row_number() OVER</c>, se
  /// fait ici en C# — ce qui laisse l'interdiction sans exception à retenir.
  /// </summary>
  [Theory]
  [MemberData(nameof(EveryQuery))]
  public void NoQueryOrders(string name, string query)
  {
    query.ShouldNotContain(
      "ORDER BY",
      Case.Insensitive,
      $"La requête {name} trie ce qu'elle lit.");
  }

  /// <summary>
  /// ⚠️ <b>Aucune requête de privilège, nulle part.</b> Le garde de privilège a été retiré, pas
  /// assoupli : le service tente la lecture et nomme ce qui se passe, il ne demande jamais la
  /// permission d'avance.
  /// </summary>
  [Theory]
  [MemberData(nameof(EveryQuery))]
  public void NoQueryAsksForAPrivilege(string name, string query)
  {
    foreach (var asking in new[] { "has_table_privilege", "has_column_privilege", "relacl", "aclexplode", "grant" })
    {
      query.ShouldNotContain(
        asking,
        Case.Insensitive,
        $"La requête {name} interroge les privilèges par {asking}.");
    }
  }

  /// <summary>
  /// ⚠️ <b>La troncature est faite par le SGBD, et la longueur réelle voyage à côté.</b> Sans le
  /// <c>left</c>, la valeur entière traverserait le réseau pour être coupée en C# ; sans le
  /// <c>length</c>, « tronqué » ne pourrait pas se dire.
  /// </summary>
  [Fact]
  public void TheSampleQueryTruncatesInTheEngineAndCarriesTheRealLength()
  {
    var query = ASampleQuery;

    query.ShouldContain("left(CAST(\"courriel\" AS text), 254)");
    query.ShouldContain("length(CAST(\"courriel\" AS text))");
  }

  /// <summary>
  /// ⚠️ <b>Cinq, et la borne se lit sur <c>ColumnPreview</c>.</b> Un <c>5</c> écrit à la main dans
  /// une requête est un second endroit où la borne vit, et deux endroits finissent par diverger.
  /// </summary>
  [Fact]
  public void TheSampleQueryStopsAtTheBoundTheDomainCarries()
  {
    var query = ASampleQuery;

    query.ShouldContain("LIMIT " + ColumnPreview.MaxValues.ToString(CultureInfo.InvariantCulture));
    query.ShouldContain(
      "AS text), " + ColumnPreview.MaxValueLength.ToString(CultureInfo.InvariantCulture) + ")");
  }

  /// <summary>
  /// ⚠️ <b>Les colonnes sont nommées, une par une, depuis le schéma relevé.</b> C'est ce qui rend
  /// l'étoile inutile plutôt que seulement interdite.
  /// </summary>
  [Fact]
  public void TheSampleQueryNamesEveryColumnItReads()
  {
    var query = ASampleQuery;

    query.ShouldContain("\"courriel\"");
    query.ShouldContain("\"meta\"");
    query.ShouldContain("\"adr_l1\"");
  }

  /// <summary>
  /// ⚠️ <b>Une requête par table, jamais deux tables dans la même.</b> Un lot ferait d'un échec de
  /// lecture sur une table l'échec des autres, et le relevé deviendrait tout ou rien.
  /// </summary>
  [Fact]
  public void TheSampleQueryReadsASingleTable()
  {
    var occurrences = ASampleQuery.Split("\"public\".\"adherents\"").Length - 1;

    occurrences.ShouldBe(1);
    ASampleQuery.ShouldNotContain("JOIN", Case.Insensitive);
    ASampleQuery.ShouldNotContain("UNION", Case.Insensitive);
  }

  /// <summary>
  /// ⚠️ <b>La liste noire est nommée type par type.</b> Une liste blanche tairait par défaut tout
  /// type qu'elle ne connaît pas encore — et un type d'extension inconnu du service n'est pas du
  /// binaire, c'est un type inconnu du service.
  /// </summary>
  [Fact]
  public void TheBlacklistNamesTheBinaryTypesOneByOne()
  {
    PostgreSqlScanQueries.UnsampleableTypes.ShouldBe(["bytea", "_bytea"], ignoreOrder: true);
  }

  /// <summary>
  /// ⚠️ <b><c>typcategory = 'U'</c> est mort : il range <c>uuid</c> et <c>jsonb</c> avec
  /// <c>bytea</c>.</b> L'employer comme filtre binaire, c'est refuser en silence de prélever un
  /// identifiant de personne et un document JSON entier — les deux colonnes que l'<c>Operator</c> a
  /// le plus besoin de voir. Une colonne PostGIS de coordonnées est de la donnée de localisation :
  /// elle se prélève, elle ne s'exclut pas.
  /// </summary>
  [Theory]
  [InlineData("uuid")]
  [InlineData("jsonb")]
  [InlineData("json")]
  [InlineData("xml")]
  [InlineData("geometry")]
  [InlineData("geography")]
  [InlineData("point")]
  [InlineData("inet")]
  [InlineData("hstore")]
  public void SamplesWhatTheUserDefinedCategoryWouldHaveSwallowed(string type)
  {
    PostgreSqlScanQueries.IsSampleable(type).ShouldBeTrue(
      $"Le type {type} est rangé par PostgreSQL dans la catégorie « U », avec bytea. "
      + "La liste noire est nommée précisément pour ne pas hériter de ce classement.");
  }

  /// <summary>Et le binaire, lui, ne se prélève pas — tableau compris.</summary>
  [Theory]
  [InlineData("bytea")]
  [InlineData("_bytea")]
  public void DoesNotSampleBinary(string type)
  {
    PostgreSqlScanQueries.IsSampleable(type).ShouldBeFalse();
  }

  /// <summary>
  /// ⚠️ <b>Aucune requête n'est bâtie sur le nom du type déclaré au pivot.</b> Le catalogue rend
  /// aussi le nom interne, domaines résolus : sans lui, un domaine posé sur <c>bytea</c> passerait
  /// la liste noire sous son propre nom et son binaire serait prélevé.
  /// </summary>
  [Fact]
  public void TheCatalogueResolvesDomainsSoTheBlacklistSeesThroughThem()
  {
    PostgreSqlScanQueries.Catalogue.ShouldContain("COALESCE(bt.typname, t.typname)");
    PostgreSqlScanQueries.Catalogue.ShouldContain("NULLIF(t.typbasetype, 0)");
  }

  /// <summary>
  /// ⚠️ <b>La lecture d'existence est une lecture d'existence.</b> Elle demande si le catalogue de
  /// schémas connaît un schéma applicatif, jamais si le compte a le droit d'y lire — c'est la seule
  /// chose qui sépare « base absente du catalogue » de « base sans table ».
  /// </summary>
  [Fact]
  public void ThePresenceQueryOnlyAsksWhetherTheCatalogueKnowsASchema()
  {
    PostgreSqlScanQueries.DatabasePresence.ShouldContain("pg_catalog.pg_namespace");
    PostgreSqlScanQueries.DatabasePresence.ShouldContain("current_database()");
  }

  /// <summary>
  /// ⚠️ <b>Un identifiant du client entre entre guillemets, ses guillemets doublés.</b> C'est le
  /// seul texte du client qui entre dans une requête, et il n'y a pas de paramètre pour un nom de
  /// table : point de revue nommé, pas une formalité.
  /// </summary>
  [Fact]
  public void AnIdentifierNeverEscapesItsQuotes()
  {
    PostgreSqlScanQueries.Quote("adhe\"rents").ShouldBe("\"adhe\"\"rents\"");
  }

  /// <summary>
  /// La routine de citation est <b>la</b> porte : un nom de table qui tenterait de refermer la
  /// requête pour en ouvrir une autre en ressort inerte, guillemets doublés compris.
  /// </summary>
  [Fact]
  public void ClosesNothingAnInjectedNameTriesToOpen()
  {
    var query = PostgreSqlScanQueries.Sample(
      "public",
      "adherents\"; DROP TABLE adherents; --",
      ["courriel"]);

    query.ShouldContain("\"adherents\"\"; DROP TABLE adherents; --\"");
    query.ShouldEndWith("LIMIT " + ColumnPreview.MaxValues.ToString(CultureInfo.InvariantCulture));
  }
}
