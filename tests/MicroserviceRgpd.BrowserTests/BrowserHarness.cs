using Mediator;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.UseCases.Configuration.SetRightEndpoint;
using MicroserviceRgpd.UseCases.Configuration.SetRightRabbitMqRouting;
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
  /// <summary>Sérialise l'installation de Chromium, que tous les harnais demandent en démarrant.</summary>
  private static readonly Lock ChromiumInstalls = new();

  private static bool _chromiumInstalled;

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
  /// <param name="reducedMotion">
  /// Le réglage système du mouvement, pour qui doit lire l'écran sous un système qui demande que rien
  /// ne bouge. Sans lui, le contexte n'exprime aucune préférence — comme un poste ordinaire.
  /// </param>
  public Task<IBrowserContext> NewContextAsync(ReducedMotion? reducedMotion = null)
  {
    return Browser.NewContextAsync(new() { BaseURL = Address.ToString(), ReducedMotion = reducedMotion });
  }

  /// <summary>
  /// Le moteur dont l'avis fait verdict, tel que le service le consulte : ce qu'un test lui dicte,
  /// l'écran le rend.
  /// </summary>
  /// <remarks>
  /// ⚠️ La doublure est partagée par toute la collection : un test qui la configure la remet à zéro
  /// d'abord, sans quoi il hériterait de l'avis du précédent.
  /// </remarks>
  public QualificationEngineDouble Verdict => Service.Verdict;

  /// <summary>Le moteur lexical, tel que le service le consulte. Même mise en garde que <see cref="Verdict"/>.</summary>
  public QualificationEngineDouble Lexicon => Service.Lexicon;

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
    for (var i = 0; i < count; i++)
    {
      await RecordRequestAsync("Martin", "Jeanne", $"{Guid.NewGuid():N}@example.org");
    }
  }

  /// <summary>
  /// Enregistre une demande valide de cette personne <b>par le use case</b>, sans passer par la
  /// modale ; rend son message unique, qui la retrouve en base. Ce qui n'est pas donné est absent, et
  /// la demande est reçue le 15/01/2026, par email, d'une identité non vérifiée, au droit d'accès.
  /// </summary>
  /// <remarks>
  /// Les quatre derniers champs se donnent aussi, pour qui doit relire une demande dont <b>aucune</b>
  /// valeur n'est celle par défaut de la modale : un pré-remplissage qui ne ferait rien se lirait
  /// alors comme une réussite.
  /// </remarks>
  public async Task<string> RecordRequestAsync(
    string lastName = "",
    string firstName = "",
    string email = "",
    string receivedOn = "2026-01-15",
    Origin? origin = null,
    bool identityVerified = false,
    string right = "Access")
  {
    using var scope = Service.Services.CreateScope();
    var message = $"Je souhaite accéder à mes données. {Guid.NewGuid()}";

    var entry = new DataSubjectRequestEntry(
      Origin: origin ?? Origin.Email,
      ReceivedOn: receivedOn,
      LastName: lastName,
      FirstName: firstName,
      Email: email,
      IdentityVerified: identityVerified,
      Message: message,
      Right: right);

    (await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new RecordDataSubjectRequestCommand(entry)))
      .IsSuccess.ShouldBeTrue();

    return message;
  }

  /// <summary>
  /// Pose la date limite de réponse de la demande enregistrée sous ce message <b>à même la table</b> —
  /// pour qui doit la voir signalée sans attendre qu'elle approche.
  /// </summary>
  public async Task SetResponseDeadlineAsync(string message, DateOnly responseDeadline)
  {
    using var scope = Service.Services.CreateScope();

    var updated = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database
      .ExecuteSqlAsync($"UPDATE data_subject_requests SET response_deadline = {responseDeadline} WHERE message = {message}");

    updated.ShouldBe(1, "La date limite n'a été posée sur aucune demande.");
  }

  /// <summary>
  /// Remplace le message de la demande enregistrée sous ce message <b>à même la table</b> — pour qui
  /// doit lire un message que la modale n'écrirait pas, sur deux lignes par exemple.
  /// </summary>
  /// <remarks>⚠️ Le message est la clé qui retrouve la demande : après cet appel, c'est le nouveau.</remarks>
  public async Task SetMessageAsync(string message, string replacement)
  {
    using var scope = Service.Services.CreateScope();

    var updated = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database
      .ExecuteSqlAsync($"UPDATE data_subject_requests SET message = {replacement} WHERE message = {message}");

    updated.ShouldBe(1, "Le message n'a été posé sur aucune demande.");
  }

  /// <summary>
  /// Pose le statut de la demande enregistrée sous ce message <b>à même la table</b> — pour qui doit
  /// voir une demande close, qu'aucun <c>Gesture</c> ne sait encore produire.
  /// </summary>
  /// <remarks>
  /// La colonne tient le <b>nom canonique</b> du statut, et non son rang : c'est ce que la
  /// configuration d'entité y écrit.
  /// </remarks>
  public async Task SetStatusAsync(string message, RequestStatus status)
  {
    ArgumentNullException.ThrowIfNull(status);

    using var scope = Service.Services.CreateScope();

    var updated = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database
      .ExecuteSqlAsync($"UPDATE data_subject_requests SET status = {status.Name} WHERE message = {message}");

    updated.ShouldBe(1, "Le statut n'a été posé sur aucune demande.");
  }

  /// <summary>
  /// Retire de la base la demande enregistrée sous ce message — <b>dans le dos de l'écran</b>, comme
  /// le ferait un second onglet.
  /// </summary>
  public async Task DeleteRequestAsync(string message)
  {
    using var scope = Service.Services.CreateScope();

    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database
      .ExecuteSqlAsync($"DELETE FROM data_subject_requests WHERE message = {message}");
  }

  /// <summary>
  /// Retire <b>toutes</b> les demandes enregistrées — pour qui doit arriver au tableau vide.
  /// </summary>
  /// <remarks>
  /// ⚠️ Ne vaut que parce que toute la collection s'exécute en série, et qu'aucun test n'y compte sur
  /// une demande qu'il n'a pas enregistrée lui-même.
  /// </remarks>
  public async Task DeleteAllRequestsAsync()
  {
    using var scope = Service.Services.CreateScope();

    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database
      .ExecuteSqlAsync($"DELETE FROM data_subject_requests");
  }

  /// <summary>
  /// Pose une adresse au droit <paramref name="right"/> <b>par le use case du Paramétrage</b> — pour
  /// qui doit voir une demande exécutable.
  /// </summary>
  public async Task ConfigureEndpointAsync(DataSubjectRight right, string endpoint = "https://brocanto.example.fr/rgpd")
  {
    using var scope = Service.Services.CreateScope();

    (await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new SetRightEndpointCommand(right, EndpointUrl.From(endpoint))))
      .IsSuccess.ShouldBeTrue();
  }

  /// <summary>
  /// Route le droit <paramref name="right"/> sur RabbitMQ <b>par le use case du Paramétrage</b> — pour
  /// qui doit voir un droit configuré que le service ne sait pas encore exercer.
  /// </summary>
  public async Task RouteOnRabbitMqAsync(DataSubjectRight right, RabbitMqRouting routing)
  {
    using var scope = Service.Services.CreateScope();

    (await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new SetRightRabbitMqRoutingCommand(right, routing)))
      .IsSuccess.ShouldBeTrue();
  }

  /// <summary>
  /// Retire la ligne du Paramétrage : le service revient à son état d'installation, chaque droit
  /// « non configuré ».
  /// </summary>
  /// <remarks>
  /// ⚠️ Le Paramétrage est un singleton partagé par toute la collection : qui en pose une adresse la
  /// retire en sortant.
  /// </remarks>
  public async Task ForgetEveryEndpointAsync()
  {
    using var scope = Service.Services.CreateScope();

    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Settings>().ExecuteDeleteAsync();
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

  /// <summary>La chaîne de connexion de la base du service, pour qui démarre un service de plus.</summary>
  internal string ConnectionString => _database.GetConnectionString();

  private IBrowser Browser => _browser ?? throw new InvalidOperationException("Le navigateur n'est pas lancé.");

  private ServiceOnARealPort Service => _service ?? throw new InvalidOperationException("Le service n'est pas démarré.");

  private Uri Address => _address ?? throw new InvalidOperationException("Le service n'est pas démarré.");

  /// <summary>
  /// ⚠️ <b>Une seule fois par processus, et sous verrou.</b> Les harnais des collections démarrent
  /// de front : deux pilotes qui téléchargent dans le même cache se marchent dessus, et le premier
  /// lancement d'un poste neuf échouerait sur une moitié de navigateur.
  /// </summary>
  private static void InstallChromium()
  {
    lock (ChromiumInstalls)
    {
      if (_chromiumInstalled)
      {
        return;
      }

      var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);

      exitCode.ShouldBe(0, "L'installation de Chromium par le pilote Playwright a échoué.");

      _chromiumInstalled = true;
    }
  }
}
