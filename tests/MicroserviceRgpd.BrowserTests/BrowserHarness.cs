using Mediator;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.UseCases.Requests.RecordDataSubjectRequest;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace MicroserviceRgpd.BrowserTests;

/// <summary>
/// <b>Un Chromium devant le service</b> : PostgreSQL en conteneur, le service sur un vrai port, et un
/// navigateur sans tête qui l'atteint par cette adresse. Démarré une fois pour toute la collection.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Chromium s'installe ici, et nulle part ailleurs.</b> Le pilote que le paquet Playwright copie
/// à côté des binaires est appelé en code : ni <c>pwsh</c>, ni commande à lancer à la main avant
/// <c>dotnet test</c>. Le téléchargement n'a lieu qu'une fois par poste ; les suivantes trouvent le
/// navigateur déjà en place et rendent la main aussitôt.
/// </para>
/// <para>
/// Chromium est le seul navigateur. Chaque test ouvre son propre contexte — cookies et stockage à
/// lui — et le ferme en sortant.
/// </para>
/// </remarks>
public sealed class BrowserHarness : IAsyncLifetime
{
  private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:18-alpine").Build();

  private ServiceOnARealPort? _service;
  private Uri? _address;
  private IPlaywright? _playwright;
  private IBrowser? _browser;

  public async Task InitializeAsync()
  {
    InstallChromium();

    await _database.StartAsync();

    _service = new ServiceOnARealPort(_database.GetConnectionString());
    _address = _service.Start();

    _playwright = await Playwright.CreateAsync();
    _browser = await _playwright.Chromium.LaunchAsync();
  }

  /// <summary>
  /// Un contexte de navigation neuf, dont les adresses relatives partent du service : un test écrit
  /// <c>GotoAsync("/")</c> et arrive à l'accueil.
  /// </summary>
  public Task<IBrowserContext> NewContextAsync()
  {
    return Browser.NewContextAsync(new() { BaseURL = Address.ToString() });
  }

  /// <summary>
  /// Le nombre de demandes enregistrées sous ce message, relu dans la base du service : ce qu'un
  /// scénario a laissé derrière lui, et non ce que l'écran en dit.
  /// </summary>
  /// <remarks>
  /// La table est lue en SQL, comme dans les tests fonctionnels : le message est un objet valeur, que
  /// le modèle EF ne compare pas à une chaîne.
  /// </remarks>
  public async Task<int> CountOfRequestsAsync(string message)
  {
    using var scope = Service.Services.CreateScope();

    return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database
      .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM data_subject_requests WHERE message = {message}")
      .SingleAsync();
  }

  /// <summary>
  /// Enregistre <paramref name="count"/> demandes valides <b>par le use case</b>, sans passer par la
  /// modale : ce qu'un scénario demande ici est un tableau qui déborde, pas une saisie.
  /// </summary>
  public async Task RecordRequestsAsync(int count)
  {
    using var scope = Service.Services.CreateScope();
    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

    for (var i = 0; i < count; i++)
    {
      var entry = new DataSubjectRequestEntry(
        Origin.Email,
        "2026-01-15",
        "Martin",
        "Jeanne",
        $"{Guid.NewGuid():N}@example.org",
        false,
        "Je souhaite accéder à mes données.",
        "Access");

      (await mediator.Send(new RecordDataSubjectRequestCommand(entry))).IsSuccess.ShouldBeTrue();
    }
  }

  public async Task DisposeAsync()
  {
    if (_browser is not null)
    {
      await _browser.DisposeAsync();
    }

    _playwright?.Dispose();

    if (_service is not null)
    {
      await _service.DisposeAsync();
    }

    await _database.DisposeAsync();
  }

  private IBrowser Browser => _browser ?? throw new InvalidOperationException("Le navigateur n'est pas lancé.");

  private ServiceOnARealPort Service => _service ?? throw new InvalidOperationException("Le service n'est pas démarré.");

  private Uri Address => _address ?? throw new InvalidOperationException("Le service n'est pas démarré.");

  private static void InstallChromium()
  {
    var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);

    exitCode.ShouldBe(0, "L'installation de Chromium par le pilote Playwright a échoué.");
  }
}
