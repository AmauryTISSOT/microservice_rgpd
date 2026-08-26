using System.Globalization;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning.MySql;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings.Scanning;

/// <summary>
/// Le <b>lint sur le texte</b> des requêtes embarquées du dialecte MariaDB/MySQL : la moitié du
/// filet de sécurité qui ne demande aucun conteneur. Une base de test peut être trop petite pour
/// qu'un <c>SELECT *</c> ou un <c>ORDER BY</c> se voie ; le texte, lui, se lit toujours.
/// </summary>
/// <remarks>
/// ⚠️ <b>Ni cette moitié ni l'autre ne suffit.</b> Le lint ne voit pas si la requête rend le bon
/// relevé, et la fixture rejouée ne voit pas ce qui coûterait cher sur dix millions de lignes.
/// C'est pour cela qu'elles sont deux.
/// </remarks>
public class MySqlScanQueriesTests
{
  /// <summary>Tout ce que le scanner MariaDB/MySQL envoie, requête de prélèvement comprise.</summary>
  public static TheoryData<string, string> EveryQuery =>
    new()
    {
      { nameof(MySqlScanQueries.DatabasePresence), MySqlScanQueries.DatabasePresence },
      { nameof(MySqlScanQueries.Catalogue), MySqlScanQueries.Catalogue },
      { nameof(MySqlScanQueries.Sample), ASampleQuery },
    };

  private static string ASampleQuery => MySqlScanQueries.Sample(
    "epreuve",
    "adherents",
    [(0, "courriel"), (1, "notes"), (2, "nom")]);

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
  /// coûterait un parcours complet de la table du client pour cinq valeurs. L'ordre du pivot se
  /// remet en C#, sur des lignes déjà lues.
  /// </summary>
  [Theory]
  [MemberData(nameof(EveryQuery))]
  public void NoQueryOrders(string name, string query)
  {
    query.ShouldNotContain(
      "ORDER BY",
      Case.Insensitive,
      $"La requête {name} trie ce que la base du client doit lui rendre.");
  }

  /// <summary>
  /// ⚠️ <b>Aucune requête ne demande de privilège, et c'est le retrait de #286 qui s'écrit ici.</b>
  /// Sur MariaDB, <c>SHOW GRANTS</c> rend la ligne <c>IDENTIFIED BY PASSWORD '&lt;condensat&gt;'</c> :
  /// le garde de privilège faisait entrer dans le service le condensat du mot de passe du compte de
  /// connexion. Il est retiré, pas assoupli — il n'y a plus de requête à contenir.
  /// </summary>
  [Theory]
  [MemberData(nameof(EveryQuery))]
  public void NoQueryAsksWhatTheAccountIsAllowedToSee(string name, string query)
  {
    query.ShouldNotContain(
      "GRANT",
      Case.Insensitive,
      $"La requête {name} demande les droits du compte. Le garde de #286 est retiré : sur MariaDB, "
      + "SHOW GRANTS rend le condensat du mot de passe, et rien ne justifie de le faire entrer.");

    query.ShouldNotContain(
      "privilege",
      Case.Insensitive,
      $"La requête {name} lit une table de privilèges.");

    query.ShouldNotContain(
      "mysql.user",
      Case.Insensitive,
      $"La requête {name} lit le catalogue des comptes du serveur.");
  }

  /// <summary>
  /// ⚠️ <b>La troncature est faite par le SGBD, et la longueur réelle voyage à côté.</b> Sans le
  /// <c>LEFT</c>, la valeur entière traverserait le réseau pour être coupée en C# ; sans le
  /// <c>CHAR_LENGTH</c>, « tronqué » ne pourrait pas se dire.
  /// </summary>
  [Fact]
  public void TheSampleQueryTruncatesInTheEngineAndCarriesTheRealLength()
  {
    var query = ASampleQuery;

    query.ShouldContain("LEFT(`courriel`, 254)");
    query.ShouldContain("CHAR_LENGTH(`courriel`)");
  }

  /// <summary>
  /// ⚠️ <b><c>CHAR_LENGTH</c>, jamais <c>LENGTH</c>.</b> <c>LENGTH</c> compte des <b>octets</b> :
  /// une adresse accentuée en UTF-8 serait annoncée plus longue qu'elle n'est, et le service dirait
  /// « tronqué » d'une valeur entière — ce qui l'écarte du compte d'une règle de format.
  /// </summary>
  [Fact]
  public void TheSampleQueryCountsCharactersAndNotBytes()
  {
    ASampleQuery.ShouldNotContain(" LENGTH(", Case.Sensitive);
    ASampleQuery.ShouldNotContain(",LENGTH(", Case.Sensitive);
  }

  /// <summary>
  /// ⚠️ <b>Les colonnes sont nommées, une par une, depuis le schéma relevé.</b> C'est ce qui rend
  /// l'étoile inutile plutôt que seulement interdite.
  /// </summary>
  [Fact]
  public void TheSampleQueryNamesEveryColumnItReads()
  {
    var query = ASampleQuery;

    query.ShouldContain("`courriel`");
    query.ShouldContain("`notes`");
    query.ShouldContain("`nom`");
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
      ", " + ColumnPreview.MaxValueLength.ToString(CultureInfo.InvariantCulture) + ")");
  }

  /// <summary>
  /// ⚠️ <b>Chaque colonne a son propre <c>LIMIT</c>.</b> Un <c>LIMIT</c> posé sur l'union entière
  /// rendrait cinq lignes pour la table, pas cinq par colonne : deux colonnes seraient servies, et
  /// la troisième déclarée sans valeur.
  /// </summary>
  [Fact]
  public void TheSampleQueryBoundsEveryColumnSeparately()
  {
    var bound = "LIMIT " + ColumnPreview.MaxValues.ToString(CultureInfo.InvariantCulture);

    (ASampleQuery.Split(bound).Length - 1).ShouldBe(3);
  }

  /// <summary>
  /// ⚠️ <b>Une requête par table, jamais deux tables dans la même.</b> Un lot ferait d'un échec de
  /// lecture sur une table l'échec des autres, et le relevé deviendrait tout ou rien.
  /// </summary>
  [Fact]
  public void TheSampleQueryReadsASingleTable()
  {
    (ASampleQuery.Split("`epreuve`.`adherents`").Length - 1).ShouldBe(3);
    ASampleQuery.ShouldNotContain("JOIN", Case.Insensitive);
  }

  /// <summary>
  /// ⚠️ <b>Un identifiant du client entre entre accents graves, ses accents graves doublés.</b>
  /// C'est le seul texte du client qui entre dans une requête, et il n'y a pas de paramètre pour un
  /// nom de table. <c>MySqlConnector</c> active <c>CLIENT_MULTI_STATEMENTS</c> inconditionnellement :
  /// un identifiant mal encadré n'ouvrirait pas une erreur de syntaxe, mais une seconde instruction.
  /// </summary>
  [Fact]
  public void AnIdentifierNeverEscapesItsQuotes()
  {
    MySqlScanQueries.Quote("ad`herents").ShouldBe("`ad``herents`");
  }

  /// <summary>
  /// <b>Le garde mord, et on le prouve.</b> On lui présente le nom qu'un serveur hostile écrirait
  /// dans son propre catalogue pour faire ouvrir une seconde instruction, et la routine le rend
  /// inerte : le point-virgule reste <b>dans</b> l'identifiant.
  /// </summary>
  [Fact]
  public void LeavesNoRoomForASecondStatement()
  {
    var query = MySqlScanQueries.Sample(
      "epreuve",
      "adherents`; DROP TABLE `adherents",
      [(0, "courriel")]);

    query.ShouldContain("`adherents``; DROP TABLE ``adherents`");
    query.ShouldNotContain("; DROP TABLE `adherents`", Case.Sensitive);
  }

  /// <summary>
  /// ⚠️ <b>La liste noire est nommée type par type.</b> Une liste blanche ferait taire, par défaut,
  /// tout type qu'elle ne connaît pas encore — la colonne <c>inet6</c> qu'une version de MariaDB
  /// ajoutera serait rendue « non prélevable » sans que personne ne l'ait décidé.
  /// </summary>
  [Fact]
  public void NamesEveryTypeItRefusesAndRefusesNothingElse()
  {
    MySqlScanQueries.UnsampleableTypes.Order(StringComparer.Ordinal).ShouldBe(
      [
        "binary",
        "blob",
        "geometry",
        "geometrycollection",
        "linestring",
        "longblob",
        "mediumblob",
        "multilinestring",
        "multipoint",
        "multipolygon",
        "point",
        "polygon",
        "tinyblob",
        "varbinary",
      ],
      Case.Sensitive,
      "La liste noire a changé. Chaque ligne dit qu'une colonne du client ne sera JAMAIS regardée : "
      + "n'y entre que ce que le serveur rend en octets. Un type qu'on ne sait pas lire n'y a pas "
      + "sa place — il se lit, et c'est le moteur qui dira ce qu'il vaut.");
  }

  /// <summary>
  /// ⚠️ <b>Le conteneur libre est prélevé, et le nom que le moteur lui donne n'y change rien.</b>
  /// <c>DATA_TYPE</c> rend <c>json</c> sur MySQL et <c>longtext</c> sur MariaDB — MariaDB
  /// n'implémente <c>JSON</c> que comme un alias de <c>LONGTEXT</c>. Faire dépendre la liste noire
  /// de ce nom donnerait deux relevés différents pour la même base selon le serveur qui la sert.
  /// </summary>
  [Theory]
  [InlineData("json")]
  [InlineData("longtext")]
  [InlineData("text")]
  [InlineData("varchar")]
  [InlineData("inet6")]
  public void SamplesTheFreeContainerWhateverTheEngineCallsIt(string dataType)
  {
    MySqlScanQueries.IsSampleable(dataType).ShouldBeTrue();
  }

  /// <summary>
  /// ⚠️ <b>Les types binaires sont écartés sur le type <b>déclaré</b>, pas sur la valeur.</b> C'est
  /// la différence avec SQLite : ici, une colonne <c>BLOB</c> ne porte que des octets, et l'écarter
  /// avant la requête épargne à la base du client une lecture pour rien.
  /// </summary>
  [Theory]
  [InlineData("blob")]
  [InlineData("BLOB")]
  [InlineData("longblob")]
  [InlineData("binary")]
  [InlineData("varbinary")]
  public void RefusesEveryBinaryContainer(string dataType)
  {
    MySqlScanQueries.IsSampleable(dataType).ShouldBeFalse();
  }

  /// <summary>
  /// ⚠️ <b><c>BIT</c> se lit, malgré son nom.</b> <c>MySqlConnector</c> le rend en <c>ulong</c> et
  /// non en octets : le <c>bit(1)</c> qui sert de booléen partout est une colonne parfaitement
  /// regardable, et l'inscrire dans la liste noire la condamnerait sur la foi de son nom.
  /// </summary>
  [Theory]
  [InlineData("bit")]
  [InlineData("BIT")]
  public void SamplesABitBecauseTheDriverRendersItAsANumber(string dataType)
  {
    MySqlScanQueries.IsSampleable(dataType).ShouldBeTrue();
  }

  /// <summary>
  /// La lecture d'existence est une lecture d'existence : elle demande si le catalogue de schémas
  /// connaît cette base, et le nom y entre en <b>paramètre</b>.
  /// </summary>
  [Fact]
  public void ThePresenceQueryOnlyAsksWhetherTheCatalogueKnowsTheDatabase()
  {
    MySqlScanQueries.DatabasePresence.ShouldContain("information_schema.SCHEMATA");
    MySqlScanQueries.DatabasePresence.ShouldContain(MySqlScanQueries.DatabaseParameter);
  }

  /// <summary>
  /// ⚠️ <b>Le nom de la base entre en paramètre dans les deux requêtes fixes.</b> Il vient de la
  /// chaîne saisie par l'<c>Operator</c> : il n'y a aucune raison de le faire entrer dans le texte
  /// quand le protocole sait le porter à part.
  /// </summary>
  [Fact]
  public void TheCatalogueQueryTakesTheDatabaseAsAParameter()
  {
    MySqlScanQueries.Catalogue.ShouldContain(MySqlScanQueries.DatabaseParameter);
  }

  /// <summary>
  /// ⚠️ <b>Le <c>type</c> du pivot est <c>COLUMN_TYPE</c>, et la liste noire lit <c>DATA_TYPE</c>.</b>
  /// Les deux voyagent, et les confondre écrirait <c>varchar</c> là où le chemin collé écrit
  /// <c>varchar(255)</c> : deux relevés de la même base cesseraient de se ressembler.
  /// </summary>
  [Fact]
  public void TheCatalogueQueryCarriesBothTheFullTypeAndItsFamily()
  {
    MySqlScanQueries.Catalogue.ShouldContain("c.COLUMN_TYPE");
    MySqlScanQueries.Catalogue.ShouldContain("c.DATA_TYPE");
  }
}
