using System.Globalization;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning.Sqlite;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings.Scanning;

/// <summary>
/// Le <b>lint sur le texte</b> des requêtes embarquées : la moitié du filet de sécurité qui ne
/// demande aucun conteneur. Une base de test peut être trop petite pour qu'un <c>SELECT *</c> ou un
/// <c>ORDER BY</c> se voie ; le texte, lui, se lit toujours.
/// </summary>
/// <remarks>
/// ⚠️ <b>Ni cette moitié ni l'autre ne suffit.</b> Le lint ne voit pas si la requête rend le bon
/// relevé, et la fixture ne voit pas ce qui coûterait cher sur dix millions de lignes. C'est pour
/// cela qu'elles sont deux.
/// </remarks>
public class SqliteScanQueriesTests
{
  /// <summary>
  /// Tout ce que le scanner SQLite envoie : les requêtes fixes, et une requête de prélèvement telle
  /// qu'elle est réellement construite.
  /// </summary>
  public static TheoryData<string, string> EveryQuery =>
    new()
    {
      { nameof(SqliteScanQueries.DatabasePresence), SqliteScanQueries.DatabasePresence },
      { nameof(SqliteScanQueries.Catalogue), SqliteScanQueries.Catalogue },
      { nameof(SqliteScanQueries.Sample), ASampleQuery },
    };

  private static string ASampleQuery => SqliteScanQueries.Sample(
    "main",
    "abonne",
    [(0, "courriel"), (1, "photo"), (2, "nom")]);

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
  /// ⚠️ <b>Pas d'<c>ORDER BY</c>.</b> Le biais du premier venu est énoncé, pas corrigé : trier
  /// coûterait un parcours complet de la table du client pour cinq valeurs.
  /// </summary>
  [Theory]
  [MemberData(nameof(EveryQuery))]
  public void NoQueryOrders(string name, string query)
  {
    query.ShouldNotContain(
      "ORDER BY",
      Case.Insensitive,
      $"La requête {name} trie ce qu'elle prélève.");
  }

  /// <summary>
  /// ⚠️ <b>La troncature est faite par le SGBD, et la longueur réelle voyage à côté.</b> Sans le
  /// <c>substr</c>, la valeur entière traverserait le réseau pour être coupée en C# ; sans le
  /// <c>length</c>, « tronqué » ne pourrait pas se dire.
  /// </summary>
  [Fact]
  public void TheSampleQueryTruncatesInTheEngineAndCarriesTheRealLength()
  {
    var query = ASampleQuery;

    query.ShouldContain("substr(\"courriel\", 1, 254)");
    query.ShouldContain("length(\"courriel\")");
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
    query.ShouldContain("\"photo\"");
    query.ShouldContain("\"nom\"");
  }

  /// <summary>
  /// ⚠️ <b>Le binaire s'écarte par liste noire, au grain de la valeur.</b> Sous SQLite le type est
  /// porté par la valeur, pas par la colonne : une liste blanche des types déclarés tairait, par
  /// défaut, tout ce qu'elle ne connaît pas encore.
  /// </summary>
  [Fact]
  public void TheSampleQueryExcludesBinaryValueByValue()
  {
    ASampleQuery.ShouldContain("typeof(\"photo\") <> 'blob'");
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
    query.ShouldContain(", 1, " + ColumnPreview.MaxValueLength.ToString(CultureInfo.InvariantCulture) + ")");
  }

  /// <summary>
  /// ⚠️ <b>Une requête par table, jamais deux tables dans la même.</b> Un lot ferait d'un échec de
  /// lecture sur une table l'échec des autres, et le relevé deviendrait tout ou rien.
  /// </summary>
  [Fact]
  public void TheSampleQueryReadsASingleTable()
  {
    var occurrences = ASampleQuery.Split("\"main\".\"abonne\"").Length - 1;

    occurrences.ShouldBe(4);
    ASampleQuery.ShouldNotContain("JOIN", Case.Insensitive);
  }

  /// <summary>
  /// ⚠️ <b>La sentinelle sépare les deux zéros.</b> Sans elle, « table vide » et « colonne dont
  /// tout est binaire » se liraient pareil : zéro ligne.
  /// </summary>
  [Fact]
  public void TheSampleQueryAsksWhetherTheTableCarriesAnyRowAtAll()
  {
    var sentinel = SqliteScanQueries.SentinelIndex.ToString(CultureInfo.InvariantCulture);

    ASampleQuery.ShouldStartWith("SELECT " + sentinel + " AS colonne");
  }

  /// <summary>
  /// ⚠️ <b>Un identifiant du client entre entre guillemets, ses guillemets doublés.</b> C'est le
  /// seul texte du client qui entre dans une requête, et il n'y a pas de paramètre pour un nom de
  /// table.
  /// </summary>
  [Fact]
  public void AnIdentifierNeverEscapesItsQuotes()
  {
    SqliteScanQueries.Quote("ab\"onne").ShouldBe("\"ab\"\"onne\"");
  }

  /// <summary>
  /// ⚠️ <b>La lecture d'existence est une lecture d'existence.</b> Elle demande si le catalogue de
  /// schémas connaît cette base, jamais si le compte a le droit d'y lire.
  /// </summary>
  [Fact]
  public void ThePresenceQueryOnlyAsksWhetherTheCatalogueKnowsTheDatabase()
  {
    SqliteScanQueries.DatabasePresence.ShouldContain("pragma_database_list");
    SqliteScanQueries.DatabasePresence.ShouldNotContain("privilege", Case.Insensitive);
    SqliteScanQueries.DatabasePresence.ShouldNotContain("grant", Case.Insensitive);
  }
}
