using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Web.Pages.Screenings;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// <b>Les requêtes que le service sert produisent-elles un relevé que le service accepte ?</b>
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce fichier existe parce que la réponse a été « non » pendant tout un temps sans que rien ne
/// s'en aperçoive</b> ([#284](https://github.com/AmauryTISSOT/microservice_rgpd/issues/284)). Les
/// trois requêtes fermaient sur <c>{"colonnes":N}</c> — sans le <c>"fin":true</c> qu'exige
/// <see cref="ColumnListingIngestion"/> — et rendaient <c>nullable</c> en <c>0</c>/<c>1</c> plutôt
/// qu'en booléen JSON. L'<c>Operator</c> se voyait donc servir une requête dont la sortie était
/// refusée en bloc, sur les quatre SGBD.
/// </para>
/// <para>
/// <b>Le défaut a survécu parce que rien ne mettait les deux morceaux face à face.</b>
/// <see cref="ListingQuery"/> était éprouvé pour être <i>embarqué</i>,
/// <see cref="ColumnListingIngestion"/> pour ses neuf refus, et personne ne jouait la requête
/// servie pour en passer la sortie à l'ingestion. Le filet a donc <b>deux moitiés</b>, et il faut
/// les deux :
/// </para>
/// <para>
/// <b>(a) le rejeu</b> — <see cref="AcceptsWhatEachServedQueryReallyProduced"/> — passe à l'ingestion
/// les octets réellement sortis des conteneurs. C'est la moitié honnête, mais elle est <b>gelée</b> :
/// elle ne rejoue pas la requête, si bien que modifier <c>mariadb.sql</c> sans réextraire la
/// laisserait verte sur un fichier cassé.
/// </para>
/// <para>
/// <b>(b) le lint</b> — <see cref="KeepsTheBooleanShapeInTheTextOfEveryServedQuery"/> — lit le
/// <b>texte</b> des requêtes telles qu'elles sont embarquées. C'est ce que (a) ne peut pas attraper.
/// Il ne prouve pas que la sortie est bonne ; il attrape la régression exacte de #284, qui est une
/// régression d'écriture.
/// </para>
/// <para>
/// ⚠️ <b>Un test à conteneurs — le plus honnête des trois — est délibérément écarté.</b> Il
/// rejouerait la requête pour de vrai à chaque build et couvrirait les deux moitiés d'un coup, au
/// prix d'un précédent lourd pour un correctif de deux lignes. Le jour où le workflow connecté
/// arrivera, c'est la première chose à reconsidérer.
/// </para>
/// </remarks>
public class ServedListingQueries
{
  /// <summary>
  /// Les captures et le dialecte que chacune doit déclarer. ⚠️ <b>MySQL et MariaDB sont deux
  /// captures pour une seule requête</b> : c'est la ligne où les deux moteurs pouvaient diverger —
  /// <c>(est_nullable = 'YES')</c> et <c>(1 = 1)</c> devaient rendre un booléen JSON sur les
  /// <b>deux</b> — et n'en éprouver qu'un laisserait l'autre au hasard.
  /// </summary>
  public static TheoryData<string, string> Captures =>
    new()
    {
      { "releve-postgresql.txt", "postgresql" },
      { "releve-mysql84.txt", "mariadb" },
      { "releve-mariadb118.txt", "mariadb" },
      { "releve-sqlite.txt", "sqlite" },
    };

  /// <summary>
  /// <b>La sortie réelle de chaque requête servie passe l'ingestion, entière.</b>
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>La capture est ingérée telle quelle, sans nettoyage préalable.</b> Filtrer ou rapiécer ici
  /// ferait dire au test « la sortie réelle passe l'ingestion » alors qu'il validerait ce que le
  /// filtre a bien voulu laisser passer — la fausse assurance exacte qui a produit #284. Les
  /// captures sont versées propres par <c>capter.sh</c>, pas nettoyées à la lecture.
  /// </remarks>
  [Theory]
  [MemberData(nameof(Captures))]
  public void AcceptsWhatEachServedQueryReallyProduced(string capture, string dialect)
  {
    var outcome = ColumnListingIngestion.Ingest(Capture(capture));

    outcome.Refusal.ShouldBeNull(
      $"La requête servie pour « {dialect} » a réellement produit ce relevé, et le service le "
      + "refuse. C'est le défaut de #284 : l'Operator reçoit une requête cassée. Réextraire la "
      + "capture après avoir corrigé la requête — voir exploration/releves-authentiques/README.md.");

    outcome.Listing!.Dialect.ShouldBe(dialect);
    outcome.Listing.Columns.ShouldNotBeEmpty();
  }

  /// <summary>
  /// <b><c>nullable</c> porte les deux valeurs, et ce sont des booléens.</b>
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Sans ce contrôle, un <c>nullable</c> constant passerait.</b> Une requête qui rendrait
  /// <c>true</c> partout serait acceptée par l'ingestion — la forme est bonne — et désactiverait le
  /// filtre de nullabilité sur toute la base sans un mot. Les bases d'épreuve portent exprès des
  /// colonnes des deux sortes ; exiger les deux valeurs est ce qui distingue « le booléen est lu »
  /// de « le booléen est écrit ».
  /// </remarks>
  [Theory]
  [MemberData(nameof(Captures))]
  public void CarriesBothNullabilitiesAsRealBooleans(string capture, string dialect)
  {
    var columns = ColumnListingIngestion.Ingest(Capture(capture)).Listing!.Columns;

    columns.ShouldContain(
      column => column.IsNullable == true,
      $"Aucune colonne nullable dans le relevé « {dialect} », alors que la base d'épreuve en porte.");

    columns.ShouldContain(
      column => column.IsNullable == false,
      $"Aucune colonne NOT NULL dans le relevé « {dialect} » : un `nullable` constant se lit comme "
      + "un filtre qui marche, et n'en est pas un.");
  }

  /// <summary>
  /// <b>Le texte des trois requêtes embarquées porte encore la forme booléenne.</b>
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Ce test lit du SQL, et c'est assumé.</b> Il ne dit pas ce que la requête produit — les
  /// captures s'en chargent. Il dit que <b>personne n'a réécrit la ligne piégée</b> sans réextraire,
  /// ce que les captures gelées ne peuvent structurellement pas voir. Les deux ensemble couvrent la
  /// surface ; l'une sans l'autre laisse un trou par lequel #284 revient.
  /// <para>
  /// ⚠️ <b>Les trois requêtes divergent sur cette ligne, et c'est voulu</b> : <c>true</c> nu sur
  /// PostgreSQL, <c>(1 = 1)</c> sur MariaDB, <c>json('true')</c> sur SQLite — qui n'a aucun type
  /// booléen et rendrait <c>1</c> pour les deux autres formes. Le lint n'impose donc pas une forme
  /// unique : il exige que chaque requête porte <b>la sienne</b>, celle que son en-tête explique.
  /// </para>
  /// </remarks>
  [Fact]
  public void KeepsTheBooleanShapeInTheTextOfEveryServedQuery()
  {
    foreach (var query in ListingQuery.All)
    {
      var closing = ClosingLineOf(query);

      closing.ShouldContain(
        "'fin'",
        Case.Sensitive,
        $"La ligne de fin de « {query.Dialect} » ne porte pas la clé `fin`. Sans elle, l'ingestion "
        + "refuse le relevé entier (MissingClosingLine) et « relevé complet » se lit comme "
        + "« copier-coller tronqué ». C'est la moitié n° 1 de #284.");

      NullableExpressionOf(query).ShouldNotMatch(
        @"\b(0|1)\b",
        $"Le `nullable` de « {query.Dialect} » est écrit avec un `0`/`1`. Le pivot exige un booléen "
        + "JSON : un nombre fait refuser chaque ligne de colonne (UnreadableColumnLine). C'est la "
        + "moitié n° 2 de #284. Lisez l'en-tête du fichier avant de « simplifier » cette ligne — la "
        + "forme diffère d'un SGBD à l'autre pour une raison attestée.");
    }
  }

  /// <summary>
  /// La ligne de fin telle qu'elle est écrite dans la requête : ce qui va du <c>json_object</c>
  /// final jusqu'à sa clé <c>colonnes</c>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Découpé en clair plutôt qu'en une expression régulière.</b> Les trois requêtes écrivent
  /// leur booléen différemment — <c>true</c>, <c>(1 = 1)</c>, <c>json('true')</c> — et une regex
  /// assez lâche pour accepter les trois est illisible, quand une regex serrée casse à la première
  /// forme non prévue. Ce lint doit survivre à une quatrième.
  /// </remarks>
  private static string ClosingLineOf(ListingQuery query)
  {
    var count = query.Sql.LastIndexOf("'colonnes'", StringComparison.Ordinal);
    var opening = count < 0
      ? -1
      : query.Sql.LastIndexOf("object(", count, StringComparison.OrdinalIgnoreCase);

    opening.ShouldBeGreaterThanOrEqualTo(
      0,
      $"La requête « {query.Dialect} » n'a plus de ligne de fin reconnaissable. Si sa forme a "
      + "changé, ce lint doit changer avec elle — pas être supprimé : il est la seule chose qui "
      + "voie une requête réécrite sans réextraction.");

    return query.Sql[opening..count];
  }

  /// <summary>
  /// L'expression que la requête donne à <c>nullable</c>, jusqu'à la clé suivante.
  /// </summary>
  private static string NullableExpressionOf(ListingQuery query)
  {
    var match = Regex.Match(
      query.Sql,
      @"'nullable'\s*,\s*(?<valeur>.*?),\s*\r?\n",
      RegexOptions.Singleline,
      TimeSpan.FromSeconds(5));

    match.Success.ShouldBeTrue(
      $"La requête « {query.Dialect} » n'écrit plus de champ `nullable` reconnaissable, alors que "
      + "le pivot exige ses neuf clés.");

    return match.Groups["valeur"].Value;
  }

  /// <summary>
  /// Une capture authentique, telle qu'elle est versionnée. Voir
  /// <c>exploration/releves-authentiques/README.md</c>.
  /// </summary>
  private static string Capture(string name)
  {
    using var stream = typeof(ServedListingQueries).GetTypeInfo().Assembly
      .GetManifestResourceStream(name)
      ?? throw new InvalidOperationException(
        $"La capture « {name} » n'est pas embarquée dans les tests. Sans elle, rien ne vérifie que "
        + "la requête servie produit un relevé que le service accepte.");

    using var reader = new StreamReader(stream, Encoding.UTF8);

    return reader.ReadToEnd();
  }
}
