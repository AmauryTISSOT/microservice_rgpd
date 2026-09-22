using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// <b>L'arbitrage en liste et détail</b> : les trois temps du parcours, l'avancement du rapport
/// entier, la liste des tables à gauche de la table ouverte, et la table suivante au pied.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Tout se lit sur des écrans rendus sans script.</b> Le module qui poste sans recharger ne
/// fait que remplacer des régions par ce que le serveur a rendu : si ces écrans-ci disent juste,
/// l'écran enrichi dit juste.
/// </para>
/// <para>
/// <b>La collection est partagée</b> : chaque test dépose son propre relevé, et lit ce que
/// <b>son</b> dépôt vient de rendre courant.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class ScreeningArbitrationWorkbench(CustomWebApplicationFactory<Program> factory)
{
  private readonly ScreeningSurface _surface = new(factory);

  /// <summary>Deux tables : une colonne signalée et une où rien n'a été vu, puis une troisième seule.</summary>
  private Task<string> TwoTablesAsync()
  {
    return _surface.DepositAndReadTheReportAsync(ScreeningSurface.Paste(
      ScreeningSurface.Column("email", position: 1),
      ScreeningSurface.Column("montant_adh", position: 2),
      ScreeningSurface.Column("montant", table: "cotisations", position: 1)));
  }

  /// <summary>
  /// <b>Le rapport dit où l'on est dans le parcours, et combien il reste</b> — en compte, jamais en
  /// mot d'état.
  /// </summary>
  [Fact]
  public async Task PlacesTheReportAtTheSecondStepAndCountsWhatIsSettled()
  {
    var report = WebUtility.HtmlDecode(await TwoTablesAsync());

    var trail = TrailOf(report);
    trail.ShouldMatch(@"(?s)<span aria-current=""page"">.*?Arbitrage\s*<span class=""trail-count"">0 / 3</span>");
    report.ShouldMatch(@"<span class=""count"">0</span>\s*/\s*3 colonnes");
    report.ShouldContain("où rien n'a été vu</strong>");
  }

  /// <summary>
  /// <b>Le rapport nomme par où commencer</b> : la première table, dans l'ordre du rapport, où une
  /// colonne attend.
  /// </summary>
  [Fact]
  public async Task NamesTheFirstTableWhereAColumnAwaits()
  {
    var report = WebUtility.HtmlDecode(await TwoTablesAsync());

    report.ShouldContain("Par où commencer");
    report.ShouldMatch(@"Ouvrir public\.adherents");
  }

  /// <summary>
  /// <b>La table ouverte se lit à côté de toutes les autres</b>, et la liste marque celle qu'on lit.
  /// </summary>
  [Fact]
  public async Task KeepsEveryTableInSightBesideTheOpenOne()
  {
    await TwoTablesAsync();

    var table = await _surface.ReadAsync(ScreeningSurface.TableOf());

    var list = Regex.Match(table, @"<nav class=""tables-list"".*?</nav>", RegexOptions.Singleline);
    list.Success.ShouldBeTrue("L'écran d'une table ne porte pas la liste des tables.");

    // ⚠️ Une seule table est marquée, et c'est celle qu'on lit.
    var marked = Regex.Matches(list.Value, @"<a[^>]*aria-current=""page""[^>]*>");
    marked.Count.ShouldBe(1);
    marked[0].Value.ShouldContain("table=adherents");

    table.ShouldContain("public.cotisations");
  }

  /// <summary>
  /// <b>Au pied de la table, le service nomme la suivante</b> — et dit que passer à la suivante ne
  /// tranche rien de ce qui attend ici.
  /// </summary>
  [Fact]
  public async Task OffersTheNextTableWithoutPretendingItSettlesThisOne()
  {
    await TwoTablesAsync();

    var table = WebUtility.HtmlDecode(await _surface.ReadAsync(ScreeningSurface.TableOf()));

    table.ShouldContain("Table suivante : public.cotisations");
    table.ShouldContain("passer à la suivante ne tranche rien");
  }

  /// <summary>
  /// ⚠️ <b>Une table tranchée en entier n'est plus la suivante de personne</b>, et elle ne quitte
  /// pas la liste : on la rouvre pour se raviser.
  /// </summary>
  [Fact]
  public async Task SkipsASettledTableWithoutDroppingItFromTheList()
  {
    await TwoTablesAsync();

    (await _surface.ArbitrateInBatchAsync(ScreenedColumnState.SetAside.Name, table: "cotisations"))
      .StatusCode.ShouldBe(HttpStatusCode.Redirect);

    var table = WebUtility.HtmlDecode(await _surface.ReadAsync(ScreeningSurface.TableOf()));

    table.ShouldNotContain("Table suivante");
    table.ShouldContain("Tranchée en entier");
    table.ShouldContain("public.cotisations");
  }

  /// <summary>
  /// <b>Quand plus rien n'attend nulle part, la suite est l'export</b>, et le rapport le dit.
  /// </summary>
  [Fact]
  public async Task LeadsToTheExportOnceNothingAwaitsAnywhere()
  {
    await _surface.DepositAndReadTheReportAsync(
      ScreeningSurface.Paste(ScreeningSurface.Column("montant")));

    (await _surface.ArbitrateInBatchAsync(ScreenedColumnState.SetAside.Name))
      .StatusCode.ShouldBe(HttpStatusCode.Redirect);

    var table = WebUtility.HtmlDecode(await _surface.ReadAsync(ScreeningSurface.TableOf()));

    table.ShouldContain("Passer à l'export");
    table.ShouldContain($"href=\"{ScreeningSurface.Export}\"");

    var report = WebUtility.HtmlDecode(await _surface.ReadAsync(ScreeningSurface.Report));

    report.ShouldContain("L'arbitrage est terminé");
    report.ShouldMatch(@"<span class=""count"">1</span>\s*/\s*1 colonne\b");
  }

  /// <summary>
  /// <b>Sur l'écran d'une table, le fil nomme la table ouverte</b> — c'est elle, la page — et son
  /// maillon ouvre chaque table du rapport, avec ce qui y est tranché.
  /// </summary>
  [Fact]
  public async Task NamesTheOpenTableAtTheEndOfTheTrailAndOpensEveryOther()
  {
    await TwoTablesAsync();

    var trail = TrailOf(WebUtility.HtmlDecode(await _surface.ReadAsync(ScreeningSurface.TableOf())));

    trail.ShouldMatch(@"<summary aria-current=""page"">\s*public\.adherents");
    trail.ShouldContain("Changer de table");
    trail.ShouldContain("table=cotisations");
    trail.ShouldMatch(@"public\.cotisations</span>\s*<span class=""trail-menu-count"">0 / 1</span>");

    // L'arbitrage n'est plus la page : il ramène au rapport.
    trail.ShouldMatch(@"<a href=""/detection"">\s*<span class=""trail-mark""[^>]*>2</span>\s*Arbitrage");
    Regex.Matches(trail, @"aria-current=""page""").Count.ShouldBe(1);
  }

  /// <summary>
  /// ⚠️ <b>Le relevé n'est un lien sur aucun écran du fil</b> : refaire un relevé range le rapport
  /// dans l'historique, et ce coût se lit au pied du rapport, pas sur un maillon. L'export, lui,
  /// reste atteignable avant la fin.
  /// </summary>
  [Fact]
  public async Task NeverLinksTheListingAndAlwaysReachesTheExport()
  {
    await TwoTablesAsync();

    foreach (var address in new[] { ScreeningSurface.Report, ScreeningSurface.TableOf(), ScreeningSurface.Export })
    {
      var trail = TrailOf(WebUtility.HtmlDecode(await _surface.ReadAsync(address)));

      trail.ShouldMatch(@"(?s)<li class=""trail-done"">\s*<span>.*?Relevé galette_prod", customMessage: address);
      trail.ShouldNotMatch(@"(?s)<a[^>]*>(?:(?!</a>).)*Relevé galette_prod", customMessage: address);
      trail.ShouldContain("Exporter", customMessage: address);
    }

    var export = TrailOf(WebUtility.HtmlDecode(await _surface.ReadAsync(ScreeningSurface.Export)));

    export.ShouldMatch(@"<span aria-current=""page"">\s*<span class=""trail-mark""[^>]*>3</span>\s*Exporter");
  }

  /// <summary>
  /// <b>Le dépôt porte le même fil</b>, réduit à ce qui existe : il n'y a pas encore de rapport.
  /// </summary>
  [Fact]
  public async Task PutsTheDepositOnTheSameTrail()
  {
    var trail = TrailOf(WebUtility.HtmlDecode(await _surface.ReadAsync(ScreeningSurface.Deposit)));

    trail.ShouldMatch(@"<a href=""/detection"">Détection</a>");
    trail.ShouldMatch(@"<span aria-current=""page"">\s*<span class=""trail-mark""[^>]*>1</span>\s*Déposer un relevé");
    trail.ShouldNotContain("Arbitrage");
  }

  private static string TrailOf(string page)
  {
    var trail = Regex.Match(page, @"<nav class=""detection-trail"".*?</nav>", RegexOptions.Singleline);
    trail.Success.ShouldBeTrue("L'écran ne porte pas le fil d'Ariane de la détection.");
    return trail.Value;
  }

  /// <summary>
  /// ⚠️ <b>La liste des tables ne porte aucune colonne</b> : elle compte, elle n'arbitre pas. Un
  /// bloc de colonne dans la liste aurait doublé l'ancre qu'un arbitrage vise.
  /// </summary>
  [Fact]
  public async Task CountsEachColumnOnceOnTheScreenOfATable()
  {
    await TwoTablesAsync();

    var table = await _surface.ReadAsync(ScreeningSurface.TableOf());

    ScreeningSurface.ColumnCountOf(table).ShouldBe(2);
    Regex.Matches(table, @"<form method=""post""").Count.ShouldBeGreaterThan(0);
  }
}
