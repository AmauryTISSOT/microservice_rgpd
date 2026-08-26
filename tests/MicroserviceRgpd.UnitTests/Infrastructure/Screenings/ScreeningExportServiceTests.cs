using System.Text;
using System.Text.Json;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Screenings;
using MicroserviceRgpd.UnitTests.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings;

/// <summary>
/// Ce que les deux fichiers de la <c>Cartographie</c> portent, et ce qu'ils ne portent pas.
/// </summary>
public class ScreeningExportServiceTests
{
  private static readonly DateTimeOffset ExportedOn =
    new(2026, 8, 26, 14, 5, 30, TimeSpan.FromHours(2));

  private readonly ScreeningExportService _export = new();

  /// <summary>
  /// Un rapport ordinaire : une signalée arbitrée, une non signalée qu'on a écartée, une qui attend.
  /// </summary>
  private static PersonalDataMap AMap()
  {
    var flagged = AScreening.AFlaggedColumn("adr_l1", position: 1);
    var setAside = ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh", position: 2));
    var awaiting = AScreening.AFlaggedColumn("dt_naiss", position: 3);

    var screening = AScreening.Of(flagged, setAside, awaiting);

    var renderedOn = new DateTimeOffset(2026, 8, 20, 11, 0, 0, TimeSpan.FromHours(2));

    screening.Arbitrate(flagged.Identity, ScreenedColumnState.Retained, renderedOn);
    screening.Arbitrate(setAside.Identity, ScreenedColumnState.SetAside, renderedOn);

    return PersonalDataMap.Of(screening, ExportedOn);
  }

  private static string CsvTextOf(ExportedFile file)
  {
    return Encoding.UTF8.GetString(file.Content, Encoding.UTF8.GetPreamble().Length, file.Content.Length - Encoding.UTF8.GetPreamble().Length);
  }

  // ─── Ce que les deux portent ────────────────────────────────────────────────────────────────

  /// <summary>
  /// ⚠️ <b>Une ligne par colonne du rapport, sans exception</b> — signalées, non signalées, retenues,
  /// écartées, en attente.
  /// </summary>
  [Fact]
  public void WritesOneRowPerColumnInBothFiles()
  {
    var map = AMap();

    using var json = JsonDocument.Parse(_export.AsJson(map).Content);

    json.RootElement.GetProperty("colonnes").GetArrayLength().ShouldBe(3);

    var rows = Rfc4180.Rows(CsvTextOf(_export.AsCsv(map)));

    rows.Count.ShouldBe(4, "L'en-tête, puis une ligne par colonne.");
    rows.Skip(1).Select(row => row[2]).ShouldBe(["adr_l1", "id_adh", "dt_naiss"]);
    rows.Skip(1).Select(row => row[14]).ShouldBe(["retenue", "écartée", "à arbitrer"]);
  }

  /// <summary>
  /// ⚠️ <b>Ni valeur lue, ni compte de valeurs, ni un mot de la clause d'incomplétude</b> — éprouvé
  /// sur un rapport <c>Scanné</c>, celui dont les aperçus vivent le plus près de l'export.
  /// </summary>
  [Fact]
  public void CarriesNoReadValueNoValueCountAndNoWordOfTheClause()
  {
    var flagged = AScreening.AFlaggedColumn();
    var screening = AScreening.OfListing(
      ListingOrigin.Scanned,
      flagged,
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh", position: 2)));

    var map = PersonalDataMap.Of(screening, ExportedOn);
    var clause = IncompletenessClause.For(screening);

    var files = new[]
    {
      Encoding.UTF8.GetString(_export.AsJson(map).Content),
      CsvTextOf(_export.AsCsv(map)),
    };

    foreach (var content in files)
    {
      // Les phrases de la clause, mot pour mot : c'est la forme exacte qu'aucun des deux fichiers
      // ne doit porter, sous aucune des cinq formes de rattrapage écartées.
      content.ShouldNotContain(clause.Perimeter.Statement);
      content.ShouldNotContain(clause.Beyond.Statement);
      content.ShouldNotContain(clause.Beyond.OpennessStatement);
      content.ShouldNotContain(clause.BeyondReach.Statement);
      content.ShouldNotContain(clause.RelationToManifest);

      // Et pas davantage un reste de vocabulaire qui la ferait deviner.
      content.ShouldNotContain("incomplet", Case.Insensitive);
      content.ShouldNotContain("Manifest", Case.Insensitive);
      content.ShouldNotContain("aperçu", Case.Insensitive);
      content.ShouldNotContain("conforme", Case.Insensitive);
    }
  }

  /// <summary>
  /// ⚠️ <b>Le JSON écrit le français en clair, et ce n'est pas du confort.</b> Le codeur par défaut
  /// de <c>Utf8JsonWriter</c> est taillé pour du JSON incrusté dans une page HTML : il aurait rendu
  /// « à arbitrer » en <c>à arbitrer</c>, illisible pour le destinataire humain — et surtout il
  /// aurait rendu <b>vide de sens</b> le garde ci-dessus, aucun texte français échappé ne se
  /// cherchant plus par sous-chaîne.
  /// </summary>
  [Fact]
  public void WritesFrenchInTheClearRatherThanAsEscapeSequences()
  {
    var content = Encoding.UTF8.GetString(_export.AsJson(AMap()).Content);

    content.ShouldContain("à arbitrer");
    content.ShouldContain("coordonnées");
    content.ShouldContain("reconnu dans « adr_l1 »");
    content.ShouldNotContain("\\u00");
  }

  // ─── Le JSON ────────────────────────────────────────────────────────────────────────────────

  /// <summary>
  /// ⚠️ <b>Le relevé est en tête, une seule fois, et rien de lui ne se répète sur la ligne</b> : les
  /// recopier cinq mille fois aurait laissé croire qu'ils peuvent varier d'une colonne à l'autre.
  /// </summary>
  [Fact]
  public void WritesTheListingOnceAndNeverOnTheRow()
  {
    var map = AMap();

    using var json = JsonDocument.Parse(_export.AsJson(map).Content);

    var listing = json.RootElement.GetProperty("releve");

    listing.GetProperty("screeningId").GetGuid().ShouldBe(map.Id.Value);
    listing.GetProperty("base").GetString().ShouldBe("galette_prod");
    listing.GetProperty("dialecte").GetString().ShouldBe("postgresql");
    listing.GetProperty("moteur").GetProperty("nom").GetString().ShouldBe(AScreening.Engine.Name);
    listing.GetProperty("moteur").GetProperty("version").GetString().ShouldBe(AScreening.Engine.Version);
    listing.GetProperty("lanceLe").GetDateTimeOffset().ShouldBe(AScreening.LaunchedOn);
    listing.GetProperty("exporteLe").GetDateTimeOffset().ShouldBe(ExportedOn);
    listing.GetProperty("comptes").GetProperty("colonnes").GetInt32().ShouldBe(3);

    foreach (var row in json.RootElement.GetProperty("colonnes").EnumerateArray())
    {
      foreach (var repeated in new[] { "base", "dialecte", "moteur", "lanceLe", "exporteLe", "releve" })
      {
        row.TryGetProperty(repeated, out _).ShouldBeFalse(
          $"« {repeated} » vit en tête : sur la ligne, il laisserait croire qu'il peut varier.");
      }
    }
  }

  /// <summary>Les seize champs, nommés comme la spec les nomme, et rien de plus.</summary>
  [Fact]
  public void WritesTheSixteenFieldsUnderTheirCamelCaseNames()
  {
    using var json = JsonDocument.Parse(_export.AsJson(AMap()).Content);

    var row = json.RootElement.GetProperty("colonnes")[0];

    row.EnumerateObject().Select(field => field.Name).ShouldBe(
      [
        "schema", "table", "colonne", "position", "type", "nullable", "commentaireColonne",
        "commentaireTable", "signalee", "categorie", "categorieLibelle", "degre", "degreLibelle",
        "motif", "etat", "renduLe",
      ]);

    // ⚠️ Le dernier champ est renduLe, en miroir d'Arbitration.RenderedOn que l'ADR-0014 a fixé.
    row.GetProperty("renduLe").GetDateTimeOffset()
      .ShouldBe(new DateTimeOffset(2026, 8, 20, 11, 0, 0, TimeSpan.FromHours(2)));

    var names = row.EnumerateObject().Select(field => field.Name).ToArray();

    names.ShouldNotContain("signataire");
    names.ShouldNotContain("signeLe");
  }

  /// <summary>
  /// ⚠️ <b>La phrase de l'inachevé est présente avec son compte quand des colonnes attendent</b>, et
  /// elle vit dans le relevé, à part — elle ne fusionne pas avec la clause, qui n'est nulle part.
  /// </summary>
  [Fact]
  public void CountsTheUnfinishedArbitrationWhenColumnsAreStillWaiting()
  {
    using var json = JsonDocument.Parse(_export.AsJson(AMap()).Content);

    json.RootElement.GetProperty("releve").GetProperty("arbitrageInacheve").GetString()
      .ShouldBe("1 colonne de cette cartographie n'a pas encore été arbitrée.");
  }

  /// <summary>
  /// ⚠️ <b>Absente, et non à zéro.</b> « 0 colonne attend encore » se lit comme une réserve alors
  /// qu'elle n'en est pas une.
  /// </summary>
  [Fact]
  public void SaysNothingOfTheUnfinishedWhenEverythingHasBeenRuledOn()
  {
    var column = AScreening.AFlaggedColumn();
    var screening = AScreening.Of(column);

    screening.Arbitrate(column.Identity, ScreenedColumnState.Retained, ExportedOn);

    using var json = JsonDocument.Parse(
      _export.AsJson(PersonalDataMap.Of(screening, ExportedOn)).Content);

    json.RootElement.GetProperty("releve").TryGetProperty("arbitrageInacheve", out _)
      .ShouldBeFalse();
  }

  /// <summary>
  /// ⚠️ <b>Les champs absents sont écrits <c>null</c> plutôt qu'omis</b> : un objet dont les clés
  /// varient d'une ligne à l'autre force un test d'existence par champ, sur un fichier dont
  /// l'absence est le régime majoritaire.
  /// </summary>
  [Fact]
  public void WritesAbsenceAsNullRatherThanAsAMissingKey()
  {
    var screening = AScreening.Of(ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh")));

    using var json = JsonDocument.Parse(
      _export.AsJson(PersonalDataMap.Of(screening, ExportedOn)).Content);

    var row = json.RootElement.GetProperty("colonnes")[0];

    row.GetProperty("degre").ValueKind.ShouldBe(JsonValueKind.Null);
    row.GetProperty("motif").ValueKind.ShouldBe(JsonValueKind.Null);
    row.GetProperty("commentaireColonne").ValueKind.ShouldBe(JsonValueKind.Null);
    row.GetProperty("renduLe").ValueKind.ShouldBe(JsonValueKind.Null);

    // Le JSON a un destinataire outillé : booléen pour un booléen, jamais « oui ».
    row.GetProperty("signalee").GetBoolean().ShouldBeFalse();
    row.GetProperty("nullable").GetBoolean().ShouldBeTrue();
  }

  // ─── Le CSV ─────────────────────────────────────────────────────────────────────────────────

  /// <summary>
  /// ⚠️ <b>Le BOM ouvre le fichier</b> : sans lui, un tableur français ouvre « prénom » en
  /// « prÃ©nom » sur un document dont tout le propos est de se lire sans outillage.
  /// </summary>
  [Fact]
  public void OpensWithTheUtf8Bom()
  {
    _export.AsCsv(AMap()).Content.Take(3).ShouldBe([0xEF, 0xBB, 0xBF]);
  }

  /// <summary>Les seize colonnes, nommées comme la spec les nomme, séparées par des points-virgules.</summary>
  [Fact]
  public void SeparatesWithSemicolonsUnderTheSixteenSnakeCaseNames()
  {
    var header = Rfc4180.Rows(CsvTextOf(_export.AsCsv(AMap())))[0];

    header.ShouldBe(
      [
        "schema", "table", "colonne", "position", "type", "nullable", "commentaire_colonne",
        "commentaire_table", "signalee", "categorie", "categorie_libelle", "degre",
        "degre_libelle", "motif", "etat", "rendu_le",
      ]);
  }

  /// <summary>
  /// ⚠️ <b>Il s'ouvre en tableau même quand un commentaire porte le séparateur, un guillemet et un
  /// retour à la ligne</b> — le cas qui, sans citation RFC 4180, coupe le fichier en deux au milieu
  /// d'une ligne. Éprouvé par une relecture, et non par une recherche de sous-chaîne.
  /// </summary>
  [Fact]
  public void StaysATableWhenACommentCarriesSemicolonsQuotesAndNewlines()
  {
    var hostile = "adresse ; postale\r\nvoir la table « adresses »\nchamp dit \"legacy\" ; à revoir";

    var column = ScreenedColumn.NothingSeen(
      AScreening.AListedColumn("adr_l1", columnComment: hostile, tableComment: "ligne 1\nligne 2"));

    var map = PersonalDataMap.Of(AScreening.Of(column), ExportedOn);

    var rows = Rfc4180.Rows(CsvTextOf(_export.AsCsv(map)));

    rows.Count.ShouldBe(2, "Un commentaire hostile ne doit pas fabriquer de lignes.");
    rows[1].Length.ShouldBe(16);
    rows[1][6].ShouldBe(hostile);
    rows[1][7].ShouldBe("ligne 1\nligne 2");
  }

  /// <summary>
  /// ⚠️ <b>Une case qui commence comme une formule est neutralisée.</b> Les commentaires, les
  /// schémas, les tables et les colonnes sont recopiés d'un relevé que le service n'a jamais
  /// vérifié, et ce fichier est fait pour être ouvert d'un double-clic puis transféré à un tiers :
  /// sans l'apostrophe de tête, un commentaire de table s'exécuterait chez le destinataire.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Citer n'aurait rien neutralisé</b> : un tableur évalue la formule citée tout autant. Le
  /// test le dit en éprouvant la case relue, guillemets défaits.
  /// </remarks>
  [Theory]
  [InlineData("=HYPERLINK(\"https://ailleurs.example\";\"cliquer\")")]
  [InlineData("+41 22 000 00 00")]
  [InlineData("- à revoir")]
  [InlineData("@adherents")]
  public void NeutralisesACellThatWouldReadAsAFormula(string hostile)
  {
    var column = ScreenedColumn.NothingSeen(
      AScreening.AListedColumn("adr_l1", columnComment: hostile));

    var map = PersonalDataMap.Of(AScreening.Of(column), ExportedOn);

    var cell = Rfc4180.Rows(CsvTextOf(_export.AsCsv(map)))[1][6];

    cell.ShouldBe("'" + hostile);
  }

  /// <summary>Un texte ordinaire ne gagne aucune apostrophe : la neutralisation ne mord que là.</summary>
  [Fact]
  public void LeavesAnOrdinaryCommentUntouched()
  {
    var column = ScreenedColumn.NothingSeen(
      AScreening.AListedColumn("adr_l1", columnComment: "adresse postale de l'adhérent"));

    var map = PersonalDataMap.Of(AScreening.Of(column), ExportedOn);

    Rfc4180.Rows(CsvTextOf(_export.AsCsv(map)))[1][6]
      .ShouldBe("adresse postale de l'adhérent");
  }

  /// <summary>
  /// ⚠️ <b>Le JSON, lui, ne neutralise rien</b>, et c'est délibéré : il n'a pas de destinataire qui
  /// évalue, et une apostrophe ajoutée y serait une <b>altération</b> du commentaire d'origine.
  /// </summary>
  [Fact]
  public void AddsNothingToTheSameCommentInTheJson()
  {
    var hostile = "=HYPERLINK(\"https://ailleurs.example\";\"cliquer\")";

    var column = ScreenedColumn.NothingSeen(
      AScreening.AListedColumn("adr_l1", columnComment: hostile));

    var map = PersonalDataMap.Of(AScreening.Of(column), ExportedOn);

    using var json = JsonDocument.Parse(_export.AsJson(map).Content);

    json.RootElement.GetProperty("colonnes")[0].GetProperty("commentaireColonne").GetString()
      .ShouldBe(hostile);
  }

  /// <summary>
  /// ⚠️ <b>Les booléens se lisent</b> : <c>true</c>/<c>false</c> auraient été deux mots anglais dans
  /// un fichier français, et <c>1</c>/<c>0</c> deux nombres qu'un tableur additionne.
  /// </summary>
  [Fact]
  public void WritesBooleansAsYesAndNo()
  {
    var rows = Rfc4180.Rows(CsvTextOf(_export.AsCsv(AMap())));

    rows[1][5].ShouldBe("oui");
    rows[1][8].ShouldBe("oui");
    rows[2][8].ShouldBe("non");
  }

  /// <summary>
  /// ⚠️ <b>L'absence est une case vide</b>, jamais le mot « null » ni un tiret : un tableur trie et
  /// filtre une case vide, il traite les deux autres comme une valeur.
  /// </summary>
  [Fact]
  public void LeavesAnEmptyCellForWhatIsAbsent()
  {
    var screening = AScreening.Of(ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh")));

    var row = Rfc4180.Rows(CsvTextOf(_export.AsCsv(PersonalDataMap.Of(screening, ExportedOn))))[1];

    row[6].ShouldBe(string.Empty);
    row[11].ShouldBe(string.Empty);
    row[13].ShouldBe(string.Empty);
    row[15].ShouldBe(string.Empty);
  }

  /// <summary>
  /// ⚠️ <b>L'horodatage du CSV perd son décalage</b> : c'est la seule forme qu'un tableur français
  /// reconnaisse comme une date plutôt que comme du texte. Le JSON, lui, garde l'ISO 8601 complet.
  /// </summary>
  [Fact]
  public void WritesTimestampsWithoutTheirOffset()
  {
    var renduLe = Rfc4180.Rows(CsvTextOf(_export.AsCsv(AMap())))[1][15];

    renduLe.ShouldBe("2026-08-20 11:00:00");
    renduLe.ShouldNotContain("+");
    renduLe.ShouldNotContain("T");
  }

  // ─── Les noms de fichiers ───────────────────────────────────────────────────────────────────

  /// <summary>
  /// Le nom porte la base et le jour, et c'est <b>tout</b> ce que le CSV dit de sa provenance.
  /// </summary>
  [Fact]
  public void NamesTheFileAfterTheDatabaseAndTheDay()
  {
    var map = AMap();

    _export.AsCsv(map).Name.ShouldBe("cartographie-galette-prod-2026-08-26.csv");
    _export.AsJson(map).Name.ShouldBe("cartographie-galette-prod-2026-08-26.json");
  }

  [Fact]
  public void AnnouncesWhatEachFileIs()
  {
    var map = AMap();

    _export.AsCsv(map).ContentType.ShouldBe("text/csv; charset=utf-8");
    _export.AsJson(map).ContentType.ShouldBe("application/json; charset=utf-8");
  }

  [Fact]
  public void RefusesAnAbsentMap()
  {
    Should.Throw<ArgumentNullException>(() => _export.AsJson(null!));
    Should.Throw<ArgumentNullException>(() => _export.AsCsv(null!));
  }
}

/// <summary>
/// Un relecteur CSV conforme à la RFC 4180, écrit ici <b>exprès</b>.
/// </summary>
/// <remarks>
/// ⚠️ <b>Chercher une sous-chaîne dans le fichier n'aurait rien prouvé.</b> Ce qui est en jeu est
/// qu'un tableur l'<b>ouvre en tableau</b> : seule une relecture qui recompose les lignes et les
/// cases peut affirmer qu'un commentaire portant <c>;</c>, un guillemet et un retour à la ligne n'a
/// pas coupé le fichier en deux.
/// </remarks>
internal static class Rfc4180
{
  internal static IReadOnlyList<string[]> Rows(string csv)
  {
    var rows = new List<string[]>();
    var row = new List<string>();
    var cell = new StringBuilder();
    var quoted = false;

    for (var index = 0; index < csv.Length; index++)
    {
      var character = csv[index];

      if (quoted)
      {
        if (character != '"')
        {
          cell.Append(character);
        }
        else if (index + 1 < csv.Length && csv[index + 1] == '"')
        {
          cell.Append('"');
          index++;
        }
        else
        {
          quoted = false;
        }

        continue;
      }

      switch (character)
      {
        case '"' when cell.Length == 0:
          quoted = true;
          break;

        case ';':
          row.Add(cell.ToString());
          cell.Clear();
          break;

        case '\r' when index + 1 < csv.Length && csv[index + 1] == '\n':
          row.Add(cell.ToString());
          cell.Clear();
          rows.Add([.. row]);
          row.Clear();
          index++;
          break;

        default:
          cell.Append(character);
          break;
      }
    }

    if (cell.Length > 0 || row.Count > 0)
    {
      row.Add(cell.ToString());
      rows.Add([.. row]);
    }

    return rows;
  }
}
