using System.Diagnostics;
using System.Globalization;
using MicroserviceRgpd.Core.Screenings;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace MicroserviceRgpd.IntegrationTests.Data.Screenings;

/// <summary>
/// Le <b>coût de la suppression en cascade</b> d'un rapport — le seul geste de cette surface qui
/// efface, et le seul dont le budget n'avait jamais été chiffré.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le budget du dépôt ne couvre pas ce geste-ci.</b> Il chiffre l'ingestion, le moteur et
/// l'écriture ; la suppression n'y figure pas, et personne n'avait mesuré ce que coûte l'effacement
/// d'un rapport de 20 000 colonnes. Un <c>Operator</c> qui clique et attend sans rien voir est un
/// <c>Operator</c> qui reclique — sur le seul geste de cette surface qu'on ne veut jamais voir posé
/// deux fois.
/// </para>
/// <para>
/// <b>La suppression est faite par le SGBD, pas par EF Core.</b> Le geste ne charge que l'en-tête du
/// rapport — jamais ses colonnes — et la contrainte <c>fk_screened_columns_screenings</c>, déclarée
/// <c>ON DELETE CASCADE</c>, emporte les filles en une instruction. C'est ce que ce test éprouve :
/// une régression qui chargerait les colonnes pour les supprimer une à une se verrait ici avant de
/// se voir en production.
/// </para>
/// <para>
/// <b>Ce que la mesure a rendu, le 13/08/2026</b>, sur PostgreSQL 18 en conteneur, machine de
/// développement, modèle et pool déjà chauds : <b>≈ 8 ms pour 20 000 colonnes</b>
/// (0,0004 ms/colonne) et <b>≈ 3 à 4 ms pour les 5 382 colonnes de Dolibarr</b> (0,001 ms/colonne),
/// stables sur deux passes.
/// </para>
/// <para>
/// ⚠️ <b>La suppression est trois ordres de grandeur moins chère que l'écriture</b> — 8 ms contre les
/// ≈ 2 700 ms que coûte la pose du même rapport — et c'est la signature attendue : l'écriture pousse
/// 20 000 lignes depuis le client, la suppression en envoie une seule et laisse le SGBD faire le
/// reste. Le geste n'a donc <b>besoin d'aucun traitement différé</b>, et l'écran qui le porte peut le
/// rendre en synchrone sans jamais faire attendre l'<c>Operator</c> : c'est ce chiffre-là qui rend le
/// « rien ne tourne en tâche de fond » tenable pour la suppression aussi.
/// </para>
/// </remarks>
[Collection(PostgreSqlCollection.Name)]
public class ScreeningDeletionCostTests(PostgreSqlFixture postgres, ITestOutputHelper output)
{
  /// <summary>Le pire cas autorisé par le contrat de format.</summary>
  private const int WorstCaseAllowed = ColumnListing.MaxColumns;

  /// <summary>La taille du plus gros schéma du corpus — celle que l'<c>Operator</c> supprimera vraiment.</summary>
  private const int DolibarrSized = 5_382;

  /// <summary>
  /// Le plafond gardé, en millisecondes par colonne — le même que celui de l'écriture, et pour la
  /// même raison : ce qui est gardé n'est pas une vitesse mais la <b>forme</b> du geste. Une cascade
  /// SGBD coûte une fraction de ce plafond ; une suppression ligne à ligne par EF Core le franchit.
  /// </summary>
  private const double MeanCeilingInMillisecondsPerColumn = 1.0;

  private static readonly DateTimeOffset LaunchedOn = new(2026, 8, 6, 9, 30, 0, TimeSpan.Zero);

  private static readonly ScreeningEngineIdentity Engine = new("lexique-fr-en", "1.0.0");

  /// <summary>
  /// <b>Supprimer un rapport reste une cascade du SGBD</b>, aux deux tailles qui comptent — et elle
  /// emporte <b>toutes</b> ses colonnes.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le rapport est chargé SANS ses colonnes</b>, exactement comme le fait le geste : les
  /// charger pour les supprimer serait la régression que ce test existe pour attraper, et un test qui
  /// les chargerait lui-même ne pourrait plus la voir.
  /// </remarks>
  [Theory]
  [InlineData(DolibarrSized)]
  [InlineData(WorstCaseAllowed)]
  public async Task DeletesAReportAndAllOfItsColumnsAsOneCascadeRatherThanOneRoundTripPerRow(int columns)
  {
    // Le même échauffement que la part écriture : EF Core compile son modèle et Npgsql ouvre son pool
    // au premier accès, et ce coût-là n'appartient pas au geste.
    await using (var warmup = postgres.NewDbContext())
    {
      var throwaway = AListingOf(1);
      warmup.Screenings.Add(throwaway);
      await warmup.SaveChangesAsync();
      warmup.Screenings.Remove(throwaway);
      await warmup.SaveChangesAsync();
    }

    var screening = AListingOf(columns);

    await using (var written = postgres.NewDbContext())
    {
      written.Screenings.Add(screening);
      await written.SaveChangesAsync();
    }

    await using var dbContext = postgres.NewDbContext();

    // ⚠️ L'en-tête seul, comme le geste : aucune colonne n'est matérialisée ici.
    var header = await dbContext.Screenings.SingleAsync(
      candidate => candidate.Id == screening.Id);

    dbContext.Screenings.Remove(header);

    var clock = Stopwatch.StartNew();
    await dbContext.SaveChangesAsync();
    clock.Stop();

    var elapsed = clock.Elapsed.TotalMilliseconds;

    // Le chiffre est porté au rapport de test : ce ticket doit consigner une mesure, pas un vert.
    output.WriteLine(string.Create(
      CultureInfo.InvariantCulture,
      $"Suppression en cascade de {columns} colonnes : {elapsed:F0} ms "
      + $"({elapsed / columns:F3} ms/colonne)."));

    // ⚠️ Et la cascade a bien tout emporté : un rapport dont les colonnes survivraient serait une
    // fuite de données personnelles qualifiées, sur un geste dont tout le propos est d'effacer.
    (await dbContext.Screenings.CountAsync(candidate => candidate.Id == screening.Id)).ShouldBe(0);
    (await dbContext.ScreenedColumns
      .CountAsync(candidate => EF.Property<ScreeningId>(candidate, "ScreeningId") == screening.Id))
      .ShouldBe(0);

    (elapsed / columns).ShouldBeLessThan(
      MeanCeilingInMillisecondsPerColumn,
      $"La suppression de {columns} colonnes a pris {elapsed:F0} ms, soit {elapsed / columns:F3} "
      + "ms/colonne : le geste a probablement cessé d'être une cascade du SGBD pour redevenir une "
      + "suppression ligne à ligne par EF Core.");
  }

  /// <summary>
  /// Un rapport de la taille qu'on lui demande, avec une ligne sur treize signalée — la densité que
  /// le corpus rend.
  /// </summary>
  private static Screening AListingOf(int columns)
  {
    var screened = new List<ScreenedColumn>(columns);

    for (var position = 0; position < columns; position++)
    {
      var listed = ListedColumn.Of(
        ColumnIdentity.Of("public", $"llx_table_{position / 13}", $"colonne_{position}"),
        (position % 13) + 1,
        "varchar(255)",
        isNullable: true,
        columnComment: null,
        tableComment: null,
        referencedTable: null);

      screened.Add(position % 13 == 0
        ? ScreenedColumn.Flagged(
          listed,
          PersonalDataCategory.ContactDetails,
          RuleStrength.Morphological,
          $"préfixe « adr » reconnu dans « colonne_{position} »")
        : ScreenedColumn.NothingSeen(listed));
    }

    return Screening.Of(
      ScreeningId.Next(),
      "dolibarr_prod",
      "postgresql",
      ListingOrigin.Pasted,
      Engine,
      columns,
      screened,
      LaunchedOn);
  }
}
