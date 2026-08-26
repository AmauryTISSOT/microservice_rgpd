using System.Diagnostics;
using System.Globalization;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Screenings;
using MicroserviceRgpd.UseCases.Screenings.RunScan;
using MicroserviceRgpd.UnitTests.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings;

/// <summary>
/// La <b>part moteur du budget</b> (#156), tenue sur un relevé de la taille du plus gros schéma du
/// corpus — 5 382 colonnes chez Dolibarr.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce n'est pas une re-mesure du banc, et ce n'est surtout pas une mesure de coût.</b> Le banc
/// a mesuré le coût sous contention, sur une machine nommée, avec un protocole écrit — p95 ≤ 0,103
/// ms/colonne pour les montages à dictionnaire, contre 29,1 ms/colonne pour le modèle CPU qui a été
/// éliminé par là. Une machine d'intégration partagée ne reproduit pas ce chiffre et n'a pas à
/// essayer.
/// </para>
/// <para>
/// <b>Ce qui est gardé ici est l'<i>ordre de grandeur</i></b>, avec une marge délibérément large :
/// le seuil est deux ordres de grandeur au-dessus de la mesure du banc. Il ne dit pas « le moteur
/// est rapide » — il dit qu'aucune régression n'a fait passer la détection d'un balayage linéaire à
/// autre chose, ce qui est le seul mode de panne qui ferait franchir la borne éliminatoire du banc
/// à un dictionnaire. Un seuil serré ferait rougir la construction un jour de machine chargée, et
/// un test qui crie faux finit ignoré.
/// </para>
/// </remarks>
public class ScreeningCostTests
{
  /// <summary>La taille du plus gros schéma du corpus, sur lequel le banc a chiffré le geste entier.</summary>
  private const int DolibarrSized = 5_382;

  /// <summary>
  /// Le plafond gardé, en millisecondes par colonne et <b>en moyenne</b> — jamais en p95, qui est
  /// une mesure du banc et le reste. Le banc a mesuré p95 ≤ 0,103 ; le portage rend environ 0,015
  /// sur une machine de développement ; on garde 1, soit dix fois la borne du banc et deux ordres
  /// de grandeur au-dessus du portage. La marge est la contrepartie assumée de mesurer sur une
  /// machine que personne n'a décrite.
  /// </summary>
  private const double MeanCeilingInMillisecondsPerColumn = 1.0;

  /// <summary>Le moteur reste linéaire dans la taille du relevé, et très loin de la borne du banc.</summary>
  [Fact]
  public async Task StaysFarUnderTheBudgetOnAListingTheSizeOfTheLargestSchema()
  {
    var engine = AScreeningEngine.Wired();
    var listing = ABigListing();

    // Un premier passage à part : le chargement des lexiques et la compilation à la volée ne sont
    // pas le geste qu'on mesure, et ils n'ont lieu qu'une fois pour la vie du service.
    await engine.ScreenAsync(listing, IScreeningEngine.NoPreviews);

    var clock = Stopwatch.StartNew();
    var screened = await engine.ScreenAsync(listing, IScreeningEngine.NoPreviews);
    clock.Stop();

    screened.Columns.Count.ShouldBe(DolibarrSized);
    (clock.Elapsed.TotalMilliseconds / DolibarrSized).ShouldBeLessThan(MeanCeilingInMillisecondsPerColumn);
  }

  /// <summary>
  /// <b>Le geste d'export tient dans le même budget</b>, sur le même relevé.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Il est gardé ici, dans la famille du moteur, et non à part</b> : c'est le second geste du
  /// contexte qui traverse <i>toutes</i> les colonnes d'un coup — le rapport n'en montre jamais plus
  /// d'une table — et le mode de panne est le même, une régression qui ferait passer d'un balayage
  /// linéaire à autre chose. La concaténation quadratique du CSV en est l'exemple exact.
  /// </para>
  /// <para>
  /// <b>Le calcul et les deux rendus sont mesurés ensemble</b>, parce que c'est ce que l'Operator
  /// attend en cliquant : mesurer le seul formateur aurait laissé hors du budget le tri des tables
  /// et la mise à plat, qui traversent les mêmes cinq mille lignes.
  /// </para>
  /// </remarks>
  [Fact]
  public async Task ExportsTheMapOfTheLargestSchemaWithinTheSameBudget()
  {
    var screened = await AScreeningEngine.Wired()
      .ScreenAsync(ABigListing(), IScreeningEngine.NoPreviews);

    var screening = Screening.Of(
      ScreeningId.Next(),
      "dolibarr_prod",
      "mysql",
      ListingOrigin.Pasted,
      screened.Engine,
      screened.Columns.Count,
      screened.Columns,
      new DateTimeOffset(2026, 8, 6, 9, 30, 0, TimeSpan.Zero));

    var export = new ScreeningExportService();
    var exportedOn = new DateTimeOffset(2026, 8, 26, 14, 5, 30, TimeSpan.FromHours(2));

    var clock = Stopwatch.StartNew();
    var map = PersonalDataMap.Of(screening, exportedOn);
    var csv = export.AsCsv(map);
    var json = export.AsJson(map);
    clock.Stop();

    map.Columns.Count.ShouldBe(DolibarrSized);
    csv.Content.Length.ShouldBeGreaterThan(DolibarrSized);
    json.Content.Length.ShouldBeGreaterThan(DolibarrSized);

    (clock.Elapsed.TotalMilliseconds / DolibarrSized)
      .ShouldBeLessThan(MeanCeilingInMillisecondsPerColumn);
  }

  /// <summary>
  /// Un relevé de la taille de Dolibarr, fait de noms réalistes : des colonnes qui déclenchent, des
  /// colonnes qui ne déclenchent pas, et des noms longs qui font travailler la découpe.
  /// </summary>
  private static ColumnListing ABigListing()
  {
    var names = new[]
    {
      "id", "email", "nom_complet", "adresseLivraison", "prix_centimes", "date_naissance",
      "commentaire_interne", "reference_psp", "mot_de_passe_hash", "libelle", "actif",
    };

    var columns = new string[DolibarrSized];

    for (var index = 0; index < DolibarrSized; index++)
    {
      var table = string.Create(CultureInfo.InvariantCulture, $"llx_table_{index / 20}");

      columns[index] = APivot.Column(
        string.Create(CultureInfo.InvariantCulture, $"{names[index % names.Length]}_{index % 7}"),
        table: table,
        position: (index % 20) + 1,
        tableComment: "table applicative");
    }

    var outcome = ColumnListingIngestion.Ingest(APivot.Paste(columns));

    outcome.Refusal.ShouldBeNull();

    return outcome.Listing!;
  }

  /// <summary>
  /// <b>Le geste du scan tient dans le même budget</b>, sur le même relevé — <b>aperçus compris</b>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Ce qui est mesuré est le geste, pas la base.</b> Le port de scan et le dépôt sont doublés
  /// et rendent tout de suite : ce qui reste est ce que le service <i>calcule</i> — l'ingestion du
  /// pivot, la détection avec les règles de forme, l'assemblage du rapport. Y laisser une entrée-
  /// sortie aurait fait de ce test une mesure du disque de la machine d'intégration.
  /// </para>
  /// <para>
  /// ⚠️ <b>Les aperçus sont là, et c'est tout l'intérêt.</b> Le chemin collé n'active pas les règles
  /// de forme ; le scan, si. Le seul geste que le budget du moteur ne couvrait pas est donc celui-ci,
  /// et le mode de panne gardé est le même : une régression qui ferait passer d'un balayage linéaire
  /// à autre chose.
  /// </para>
  /// </remarks>
  [Fact]
  public async Task RunsTheScanGestureOnTheLargestSchemaWithinTheSameBudget()
  {
    var listing = ABigListing();
    var previews = listing.Columns.ToDictionary(
      column => column.Identity,
      _ => ColumnPreview.Read([PreviewedValue.Of("Durand", 6)]));

    var scanner = Substitute.For<IDatabaseScanner>();
    scanner
      .ScanAsync(
        Arg.Any<DatabaseDialect>(),
        Arg.Any<string>(),
        Arg.Any<IProgress<ScanStep>?>(),
        Arg.Any<CancellationToken>())
      .Returns(ScanOutcome.Listed(APivotOf(listing), previews));

    var engine = AScreeningEngine.Wired();
    var gesture = new ScanGesture(
      scanner,
      engine,
      Substitute.For<IRepository<Screening>>(),
      new ScanPreviews(),
      TimeProvider.System);

    var progress = ScanProgress.Starting(ScanId.Next(), TimeProvider.System.GetUtcNow());

    // Un premier passage à part : les lexiques et la compilation à la volée ne sont pas le geste
    // qu'on mesure, et ils n'ont lieu qu'une fois pour la vie du service.
    await engine.ScreenAsync(listing, previews);

    var clock = Stopwatch.StartNew();
    await gesture.RunAsync(progress, DatabaseDialect.PostgreSql, "chaîne-de-test");
    clock.Stop();

    progress.Snapshot.Ending.ShouldBe(ScanEnding.Listed);
    progress.Snapshot.Total.ShouldBe(DolibarrSized);

    (clock.Elapsed.TotalMilliseconds / DolibarrSized)
      .ShouldBeLessThan(MeanCeilingInMillisecondsPerColumn);
  }

  /// <summary>
  /// Le pivot d'un relevé déjà ingéré, réécrit tel que le port de scan le rendrait. ⚠️ Le geste
  /// <b>réingère</b> ce que le scanner rend : lui passer un <c>ColumnListing</c> tout fait aurait
  /// mesuré un geste que le service n'exécute pas.
  /// </summary>
  private static string APivotOf(ColumnListing listing)
  {
    var columns = listing.Columns
      .Select(column => APivot.Column(
        column.Identity.Column,
        table: column.Identity.Table,
        position: column.Position,
        tableComment: "table applicative"))
      .ToArray();

    return APivot.Paste(columns);
  }
}
