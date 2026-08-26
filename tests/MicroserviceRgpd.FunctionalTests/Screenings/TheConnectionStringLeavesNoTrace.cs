using System.Diagnostics;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// <b>Le canari de la chaîne de connexion</b> : elle est remise au port de scan, et elle ne se pose
/// nulle part ailleurs — ni dans un journal, ni sur un tag de trace, ni en base, ni dans
/// l'avancement que l'écran d'attente rafraîchit toutes les trois secondes.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>C'est la première pierre, et elle est posée volontairement tôt.</b> Le service journalise
/// en structuré et trace ses requêtes : le jour où quelqu'un fera passer le lancement par un
/// message, le <c>LoggingBehavior</c> recopiera <b>toutes</b> ses propriétés dans le journal sans
/// que rien ne rougisse — sauf ce fichier.
/// </para>
/// <para>
/// ⚠️ <b>Le secret est reconnaissable et n'appartient qu'à ce test.</b> Chercher « Host=… » aurait
/// pu tomber sur la chaîne du conteneur PostgreSQL des tests, et le canari aurait rougi pour une
/// fuite qui n'est pas celle qu'il garde.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class TheConnectionStringLeavesNoTrace(CustomWebApplicationFactory<Program> factory)
{
  /// <summary>Le fragment qui n'existe que dans la chaîne de ce test.</summary>
  private const string Canary = "canari-308-ne-doit-pas-fuiter";

  private readonly CustomWebApplicationFactory<Program> _factory = factory;

  /// <summary>Elle arrive au port de scan — c'est la seule chose qu'elle doit faire.</summary>
  [Fact]
  public async Task ReachesTheScanningPortAndNothingElse()
  {
    await ScanAsync();

    _factory.Scanner.ReceivedConnectionString.ShouldBe(ScanSurface.ASecret);
  }

  /// <summary>Aucun journal ne l'écrit, ni en message rendu, ni en propriété structurée.</summary>
  [Fact]
  public async Task AppearsInNoLogTheServiceWrites()
  {
    _factory.Logs.Clear();

    await ScanAsync();

    _factory.Logs.Mention(Canary).ShouldBeFalse(
      "La chaîne de connexion a été journalisée. Un journal se recopie, se centralise et se garde "
      + "des mois : ce qui y entre n'en sort plus.");
  }

  /// <summary>Aucun tag de trace ne la porte, sur aucune <c>Activity</c> du lancement.</summary>
  /// <remarks>
  /// ⚠️ <b>Les tags de trace partent chez un tiers.</b> Un secret posé là voyage vers un collecteur
  /// que le service ne contrôle pas, et il y reste aussi longtemps que la rétention du collecteur.
  /// </remarks>
  [Fact]
  public async Task SitsOnNoActivityTag()
  {
    var tagged = new List<string>();

    using var listener = new ActivityListener
    {
      ShouldListenTo = _ => true,
      Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllData,
      ActivityStopped = activity =>
      {
        lock (tagged)
        {
          tagged.AddRange(activity.Tags.Select(tag => $"{tag.Key}={tag.Value}"));
          tagged.Add(activity.DisplayName);
        }
      },
    };

    ActivitySource.AddActivityListener(listener);

    await ScanAsync();

    lock (tagged)
    {
      tagged.ShouldNotContain(
        tag => tag.Contains(Canary, StringComparison.OrdinalIgnoreCase),
        "La chaîne de connexion est posée sur un tag de trace, qui part chez un tiers.");
    }
  }

  /// <summary>Elle n'est écrite dans aucune ligne du rapport que le scan produit.</summary>
  [Fact]
  public async Task IsWrittenNowhereInTheDatabase()
  {
    await ScanAsync();

    using var scope = _factory.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var screenings = await database.Screenings.AsNoTracking().ToListAsync();

    screenings.ShouldNotContain(
      screening => Mentions(screening.Database) || Mentions(screening.Dialect),
      "Un rapport porte la chaîne de connexion. Elle survivrait au processus, ce qu'aucun secret "
      + "d'accès confié pour la durée d'un scan ne doit faire.");

    var columns = await database.ScreenedColumns.AsNoTracking().ToListAsync();

    columns.ShouldNotContain(
      column => Mentions(column.Identity.Schema)
        || Mentions(column.Identity.Table)
        || Mentions(column.Identity.Column),
      "Une colonne détectée porte la chaîne de connexion.");
  }

  /// <summary>
  /// ⚠️ <b>Elle n'est pas dans <c>ScanProgress</c>, et il n'a aucun champ où la mettre.</b> Un écran
  /// que le navigateur rafraîchit toutes les trois secondes est le dernier endroit du service où un
  /// secret d'accès aurait sa place.
  /// </summary>
  [Fact]
  public void HasNoFieldToSitOnInTheProgressTheWaitingScreenReads()
  {
    var carried = typeof(ScanProgress).GetProperties()
      .Concat(typeof(ScanSnapshot).GetProperties())
      .Where(property => property.PropertyType == typeof(string))
      .ToList();

    carried.ShouldBeEmpty(
      "ScanProgress porte une propriété de texte : c'est un champ où une chaîne de connexion peut "
      + "se poser, sur un objet que l'écran d'attente rend toutes les trois secondes.");
  }

  /// <summary>Elle ne se lit sur aucun des deux écrans du scan.</summary>
  [Fact]
  public async Task NeverAppearsOnAScreen()
  {
    var surface = new ScanSurface(_factory);

    _factory.Scanner.Reset();
    _factory.Scanner.Outcome = DatabaseScannerDouble.AListing(("adherents", "nom"));
    _factory.Scanner.Hold();

    var scanId = await surface.LaunchAsync();

    try
    {
      (await surface.WaitingScreenAsync(scanId)).ShouldNotContain(Canary);
      (await surface.Client.GetStringAsync(ScanSurface.Connection)).ShouldNotContain(Canary);
    }
    finally
    {
      // ⚠️ La libération est dans un `finally` : une assertion qui tombe laisserait sinon la
      // doublure retenue et le scan « en vol », sur une fabrique partagée par toute la collection.
      _factory.Scanner.Release();
      await surface.UntilItEndsAsync(scanId);
    }

    (await surface.Client.GetStringAsync(ScreeningSurface.Report)).ShouldNotContain(Canary);
  }

  private static bool Mentions(string? text)
  {
    return text?.Contains(Canary, StringComparison.OrdinalIgnoreCase) == true;
  }

  private async Task ScanAsync()
  {
    await new ScanSurface(_factory).ScanAsync(
      DatabaseScannerDouble.AListing(("adherents", "nom")));
  }
}
