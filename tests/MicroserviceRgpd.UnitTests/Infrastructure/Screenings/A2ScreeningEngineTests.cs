using System.Text.Json;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Screenings;
using MicroserviceRgpd.TestDoubles.Ollama;
using MicroserviceRgpd.UnitTests.Core.Screenings;
using Microsoft.Extensions.DependencyInjection;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings;

/// <summary>
/// Le moteur A2 et le drapeau qui le câble, éprouvés <b>par le port</b> que le câblage rend, sur un
/// double d'Ollama posé au fil HTTP.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucun test ne parle à un vrai Ollama</b>, et aucun ne lit une classe interne du moteur : les
/// décisions attendues sortent du jeu figé calculé en Python par <c>a2_model.py</c>, jamais d'une
/// relecture du C#.
/// </para>
/// <para>
/// ⚠️ <b>Le score n'est jamais lu ici, parce qu'il ne sort pas du moteur.</b> L'équivalence avec
/// Python se prouve par les décisions, et notamment par deux colonnes posées à ±1e-5 du seuil.
/// </para>
/// </remarks>
public class A2ScreeningEngineTests
{
  // ─── Le drapeau ─────────────────────────────────────────────────────────────────────────────

  /// <summary><b>Absent, le drapeau vaut éteint</b> : un déploiement qui ne dit rien détecte au lexique.</summary>
  [Fact]
  public async Task DetectsWithTheLexiconWhenNothingTurnsA2On()
  {
    var screened = await AScreeningEngine.Wired().ScreenAsync(Listing(("users", "email")), IScreeningEngine.NoPreviews);

    screened.Engine.Name.ShouldBe("regles-lexique-fr-en");
  }

  /// <summary>
  /// Écrit « false », le drapeau éteint comme son absence — et les réglages d'A2 laissés derrière
  /// sont ignorés sans bruit.
  /// </summary>
  [Fact]
  public async Task DetectsWithTheLexiconWhenTheFlagSaysSoInSoManyWords()
  {
    var settings = AScreeningEngine.A2On();
    settings[ScreeningEngineServiceExtensions.EmbeddingsEnabledKey] = "false";
    var ollama = new OllamaDouble();

    var screened = await AScreeningEngine.Wired(AScreeningEngine.Configuration(settings), ollama)
      .ScreenAsync(Listing(("users", "email")), IScreeningEngine.NoPreviews);

    screened.Engine.Name.ShouldBe("regles-lexique-fr-en");
    ollama.EmbedBodies.ShouldBeEmpty();
  }

  [Fact]
  public async Task DetectsWithA2WhenTheFlagIsOn()
  {
    var screened = await AScreeningEngine.WiredToA2(new OllamaDouble())
      .ScreenAsync(Listing(("users", "email")), IScreeningEngine.NoPreviews);

    screened.Engine.Name.ShouldBe("a2-bge-m3-logreg");
  }

  /// <summary>
  /// Une valeur qui n'est ni « true » ni « false » arrête le démarrage plutôt que d'éteindre : lue
  /// comme un « non », une coquille passerait pour une décision.
  /// </summary>
  [Theory]
  [InlineData("oui")]
  [InlineData("1")]
  [InlineData("allumé")]
  public void RefusesToStartOnAFlagThatIsNeitherTrueNorFalse(string flag)
  {
    var settings = AScreeningEngine.A2On();
    settings[ScreeningEngineServiceExtensions.EmbeddingsEnabledKey] = flag;

    Should.Throw<ArgumentException>(
      () => new ServiceCollection().AddScreeningEngine(AScreeningEngine.Configuration(settings)));
  }

  /// <summary>
  /// Allumé, A2 exige l'adresse d'Ollama et son échéance : une configuration incomplète est une
  /// panne bruyante au démarrage, jamais une panne de détection au premier dépôt.
  /// </summary>
  [Theory]
  [InlineData(ScreeningEngineServiceExtensions.OllamaBaseAddressKey)]
  [InlineData(ScreeningEngineServiceExtensions.EmbeddingsDeadlineKey)]
  public void RefusesToStartWithA2OnWithoutWhatItNeedsToReachOllama(string missing)
  {
    var settings = AScreeningEngine.A2On();
    settings.Remove(missing);

    Should.Throw<ArgumentException>(
      () => new ServiceCollection().AddScreeningEngine(AScreeningEngine.Configuration(settings)));
  }

  // ─── Ce qu'A2 lit ───────────────────────────────────────────────────────────────────────────

  /// <summary>
  /// ⚠️ <b>A2 ne lit que le nom de la table et celui de la colonne</b>, au gabarit du manifest : ni
  /// schéma, ni type, ni commentaire. Et il encode <b>par lots</b>, jamais un relevé entier d'un seul
  /// appel.
  /// </summary>
  [Fact]
  public async Task SendsOnlyTheTextsOfTheManifestTemplateToTheEncoderInBatches()
  {
    var ollama = new OllamaDouble();
    var columns = Enumerable.Range(1, 130)
      .Select(i => APivot.Column(
        $"champ_{i}", table: "clients", position: i, dataType: "text", columnComment: "le secret du commentaire"))
      .ToArray();

    await AScreeningEngine.WiredToA2(ollama).ScreenAsync(Ingested(APivot.Paste(columns)), IScreeningEngine.NoPreviews);

    ollama.EmbedBodies.Count.ShouldBeGreaterThan(1, "Un relevé de 130 colonnes doit partir en plusieurs lots.");

    var sent = new List<string>();

    foreach (var body in ollama.EmbedBodies)
    {
      using var request = JsonDocument.Parse(body);

      request.RootElement.EnumerateObject().Select(property => property.Name).ShouldBe(["model", "input"], ignoreOrder: true);
      request.RootElement.GetProperty("model").GetString().ShouldBe(A2Equivalence.EncoderTag);

      var batch = request.RootElement.GetProperty("input").EnumerateArray().Select(text => text.GetString()!).ToList();
      batch.Count.ShouldBeLessThanOrEqualTo(64);
      sent.AddRange(batch);
    }

    sent.ShouldBe([.. Enumerable.Range(1, 130).Select(i => $"table: clients | column: champ_{i}")]);
  }

  /// <summary>
  /// <b>L'équivalence avec Python.</b> Sur le jeu figé, le moteur rend les décisions et les
  /// catégories que <c>a2_model.py</c> a rendues — y compris les deux colonnes posées à ±1e-5 du
  /// seuil, qu'un score écarté de plus de 1e-5 ferait passer du mauvais côté.
  /// </summary>
  [Fact]
  public async Task RendersTheDecisionsAndCategoriesThePythonModelRendered()
  {
    var frozen = A2Equivalence.Columns;

    var screened = await AScreeningEngine.WiredToA2(new OllamaDouble())
      .ScreenAsync(Listing([.. frozen.Select(column => (column.Table, column.Column))]), IScreeningEngine.NoPreviews);

    frozen.ShouldContain(column => column.IsPersonal && column.Score - A2Equivalence.Threshold < 1e-4, "Le jeu doit poser une colonne juste au-dessus du seuil.");
    frozen.ShouldContain(column => !column.IsPersonal && A2Equivalence.Threshold - column.Score < 1e-4, "Le jeu doit poser une colonne juste en dessous du seuil.");

    foreach (var (expected, line) in frozen.Zip(screened.Columns))
    {
      line.Identity.Column.ShouldBe(expected.Column);
      line.IsFlagged.ShouldBe(expected.IsPersonal, $"{expected.Table}.{expected.Column} : décision différente de Python.");

      if (expected.IsPersonal)
      {
        line.Category.PrototypeName.ShouldBe(expected.Category, $"{expected.Table}.{expected.Column} : prototype différent de Python.");
      }
    }
  }

  // ─── Ce qu'A2 rend ──────────────────────────────────────────────────────────────────────────

  /// <summary>
  /// Une ligne signalée porte la catégorie du prototype le plus proche, le degré
  /// <see cref="RuleStrength.PrototypeProximity"/>, et un motif français qui cite
  /// <c>table.colonne</c>, le texte traduit du prototype et le libellé de la catégorie.
  /// ⚠️ <b>Aucun chiffre</b> : un score dans le motif se lirait comme une probabilité.
  /// </summary>
  [Fact]
  public async Task GivesAFlaggedLineItsCategoryTheProximityDegreeAndAFrenchReasonWithoutAnyNumber()
  {
    var screened = await AScreeningEngine.WiredToA2(new OllamaDouble())
      .ScreenAsync(Listing(("users", "email")), IScreeningEngine.NoPreviews);

    var line = screened.Columns.Single();

    line.Category.ShouldBe(PersonalDataCategory.ContactDetails);
    line.Strength.ShouldBe(RuleStrength.PrototypeProximity);
    line.Reason.ShouldBe(
      "le nom « users.email » est proche du prototype « l'adresse électronique ou le numéro de "
      + "téléphone d'une personne » : coordonnées");
    Regex.IsMatch(line.Reason!, @"\d").ShouldBeFalse();
  }

  /// <summary>Sous le seuil : <c>Unflagged</c>, sans degré ni motif — « rien vu » se lit comme avant.</summary>
  [Fact]
  public async Task LeavesALineUnderTheThresholdUnflaggedWithoutDegreeOrReason()
  {
    var screened = await AScreeningEngine.WiredToA2(new OllamaDouble())
      .ScreenAsync(Listing(("products", "price")), IScreeningEngine.NoPreviews);

    var line = screened.Columns.Single();

    line.Category.ShouldBe(PersonalDataCategory.Unflagged);
    line.Strength.ShouldBeNull();
    line.Reason.ShouldBeNull();
  }

  /// <summary>
  /// ⚠️ <b>Une ligne par colonne, dans l'ordre du relevé</b>, <c>Unflagged</c> comprises : c'est le
  /// mécanisme entier de l'<c>Omission relue</c>. Deux colonnes de même nom dans deux schémas
  /// partagent un texte, et chacune garde sa ligne.
  /// </summary>
  [Fact]
  public async Task RendersOneLinePerColumnInTheOrderOfTheListing()
  {
    var listing = Ingested(APivot.Paste(
      APivot.Column("price", table: "products", position: 1),
      APivot.Column("email", table: "users", position: 1),
      APivot.Column("email", table: "users", schema: "archive", position: 1),
      APivot.Column("sku", table: "products", position: 2)));

    var screened = await AScreeningEngine.WiredToA2(new OllamaDouble()).ScreenAsync(listing, IScreeningEngine.NoPreviews);

    screened.Columns.Select(line => line.Identity).ShouldBe(listing.Columns.Select(column => column.Identity));
    screened.Columns.Select(line => line.IsFlagged).ShouldBe([false, true, true, false]);
  }

  /// <summary>
  /// ⚠️ <b>Les aperçus ne changent rien au rapport A2</b> — ni une ligne, ni l'identité : aucune
  /// valeur lue ne décide d'une catégorie, et un relevé collé se compare sans réserve à un relevé
  /// scanné.
  /// </summary>
  [Fact]
  public async Task RendersTheSameReportWhetherPreviewsAreGivenOrNot()
  {
    var listing = Listing(("users", "email"), ("products", "price"));
    var engine = AScreeningEngine.WiredToA2(new OllamaDouble());

    var previews = listing.Columns.ToDictionary(
      column => column.Identity,
      _ => ColumnPreview.Read([PreviewedValue.Of("FR7630006000011234567890189", 27)]));

    var pasted = await engine.ScreenAsync(listing, IScreeningEngine.NoPreviews);
    var scanned = await engine.ScreenAsync(listing, previews);

    scanned.Engine.ShouldBe(pasted.Engine);
    scanned.Columns.Select(Described).ShouldBe(pasted.Columns.Select(Described));
  }

  /// <summary>
  /// L'identité nomme A2 et <b>deux</b> pièces capables de bouger seules : l'artefact de modèle, par
  /// l'empreinte de son manifest, et l'encodeur servi, par son digest.
  /// </summary>
  [Fact]
  public async Task NamesItselfAfterTheManifestOfTheArtefactAndTheDigestOfTheEncoder()
  {
    var screened = await AScreeningEngine.WiredToA2(new OllamaDouble())
      .ScreenAsync(Listing(("users", "email")), IScreeningEngine.NoPreviews);

    screened.Engine.ShouldBe(new ScreeningEngineIdentity(
      "a2-bge-m3-logreg",
      $"modele-{A2Equivalence.ManifestSha256[..12]}+encodeur-{A2Equivalence.EncoderDigest[..12]}"));
  }

  private static string Described(ScreenedColumn line)
  {
    return $"{line.Identity} | {line.Category.Name} | {line.Strength?.Name} | {line.Reason}";
  }

  /// <summary>Un relevé collé qui porte ces colonnes, dans cet ordre, positions comptées par table.</summary>
  private static ColumnListing Listing(params (string Table, string Column)[] columns)
  {
    var positions = new Dictionary<string, int>();

    return Ingested(APivot.Paste([.. columns.Select(column =>
    {
      positions[column.Table] = positions.GetValueOrDefault(column.Table) + 1;

      return APivot.Column(column.Column, table: column.Table, position: positions[column.Table]);
    })]));
  }

  private static ColumnListing Ingested(string paste)
  {
    var outcome = ColumnListingIngestion.Ingest(paste);

    outcome.Refusal.ShouldBeNull();

    return outcome.Listing!;
  }
}
