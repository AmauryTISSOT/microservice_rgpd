using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MicroserviceRgpd.Infrastructure.Data;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// <b>Après le passage aux prototypes, l'historique est vide</b> — plutôt qu'une archive dont les
/// catégories n'existent plus.
/// </summary>
/// <remarks>
/// ⚠️ <b>Il a son propre hôte, donc sa propre base.</b> La collection partagée porte les rapports de
/// tous les autres tests, et rejouer la migration sur elle les emporterait en plein vol.
/// </remarks>
[Collection(AnEmptiedHistoryWebCollection.Name)]
public class ScreeningHistoryAfterThePrototypeTaxonomy(AnEmptiedHistoryWebApplicationFactory factory)
{
  /// <summary>La dernière migration écrite avant le passage aux prototypes.</summary>
  private const string BeforeTheClearing = "20260912095930_AddDataSubjectRequestModification";

  private readonly ScreeningSurface _surface = new(factory);

  [Fact]
  public async Task ShowsAnEmptyHistoryOnceTheMigrationHasRun()
  {
    await _surface.DepositAndReadTheReportAsync(
      ScreeningSurface.Paste(
        ScreeningSurface.Column("confession", position: 1),
        ScreeningSurface.Column("email", position: 2)));

    var history = await _surface.ReadAsync(ScreeningSurface.History);
    history.ShouldNotContain("Aucun rapport de détection n'a été lancé", Case.Sensitive);

    using (var scope = factory.Services.CreateScope())
    {
      var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      var migrator = database.GetService<IMigrator>();

      // La base revient à la veille, et le rapport y porte une valeur de la taxonomie d'alors.
      await migrator.MigrateAsync(BeforeTheClearing);
      await database.Database.ExecuteSqlRawAsync(
        "update screened_columns set category = 'SpecialCategoryData' where column_name = 'confession'");

      await migrator.MigrateAsync();
    }

    var emptied = await _surface.ReadAsync(ScreeningSurface.History);

    emptied.ShouldContain("Aucun rapport de détection n'a été lancé", Case.Sensitive);
  }
}
