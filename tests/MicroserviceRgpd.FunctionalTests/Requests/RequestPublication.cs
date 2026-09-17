using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace MicroserviceRgpd.FunctionalTests.Requests;

/// <summary>
/// <b>Exécuter une demande routée publie sur RabbitMQ</b>, exercé par la <b>seule frontière HTTP</b>
/// — le handler <c>POST /demandes?handler=Execute</c>, jeton anti-rejeu compris — <b>contre un vrai
/// broker en conteneur</b> (ADR-0028).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Rien n'est substitué dans le conteneur</b> : c'est le vrai client AMQP du service qui
/// publie, sur la connexion qu'il ouvre lui-même, et ce qu'une file reçoit est ce qu'un intégrateur
/// recevrait. La topologie — exchange, file, binding — est posée <b>par le test</b>, jamais par le
/// service : c'est ainsi que l'exploitant la pose.
/// </para>
/// <para>
/// ⚠️ <b>Chaque test pose sa propre topologie sous un nom unique</b> et la retire en sortant : une
/// file laissée derrière ferait réussir le test du non routable un jour sur deux.
/// </para>
/// </remarks>
[Collection(ABrokerConfiguredWebCollection.Name)]
public class RequestPublication(ABrokerConfiguredWebApplicationFactory factory) : IAsyncLifetime
{
  private readonly RequestSurface _surface = new(factory);

  /// <summary>Ce que l'exploitant a déclaré pour ce test : un exchange à lui, et rien d'autre.</summary>
  private readonly string _exchange = $"rgpd.exercice.{Guid.NewGuid():N}";

  private readonly string _queue = $"rgpd.file.{Guid.NewGuid():N}";

  private IConnection _broker = null!;

  private IChannel _topology = null!;

  public async Task InitializeAsync()
  {
    await _surface.ForgetEveryChannelAsync();

    _broker = await new ConnectionFactory
    {
      HostName = factory.HostName,
      Port = factory.Port,
      UserName = ABrokerConfiguredWebApplicationFactory.UserName,
      Password = ABrokerConfiguredWebApplicationFactory.Password,
    }.CreateConnectionAsync();

    _topology = await _broker.CreateChannelAsync();
  }

  public async Task DisposeAsync()
  {
    await _topology.ExchangeDeleteAsync(_exchange);
    await _topology.QueueDeleteAsync(_queue);
    await _topology.DisposeAsync();
    await _broker.DisposeAsync();
    await _surface.ForgetEveryChannelAsync();
  }

  /// <summary>
  /// <b>Le message arrive dans la file liée au routage du Paramétrage</b>, porte les <b>cinq mêmes
  /// clés</b> que le corps HTTP, <c>content-type: application/json</c>, un <c>message-id</c> égal à
  /// l'identifiant de la demande, et il est <b>persistant</b>.
  /// </summary>
  [Fact]
  public async Task PublishesTheFiveKeysToTheQueueBoundToTheRoutingOfTheRight()
  {
    await BindAQueueAsync("droit.effacement");
    await RouteAsync(DataSubjectRight.Erasure, "droit.effacement");
    var email = $"{Guid.NewGuid():N}@example.org";

    var (id, _) = await _surface.RecordAsync(new Dictionary<string, string>
    {
      ["email"] = email,
      ["lastName"] = "Martin",
      ["firstName"] = "Jeanne",
      ["identityVerified"] = "true",
      ["right"] = nameof(DataSubjectRight.Erasure),
    });

    var response = await _surface.ExecuteAsync(id);
    response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

    var received = await TheOnlyMessageInTheQueueAsync();

    received.BasicProperties.ContentType.ShouldBe("application/json");
    received.BasicProperties.MessageId.ShouldBe(id.ToString(), "Le message-id n'est pas l'identifiant de la demande.");
    received.BasicProperties.Persistent.ShouldBeTrue("Le message n'est pas persistant.");

    using var body = JsonDocument.Parse(Encoding.UTF8.GetString(received.Body.Span));

    body.RootElement.EnumerateObject().Select(property => property.Name)
      .ShouldBe(["requestId", "right", "email", "firstName", "lastName"], ignoreOrder: true);
    body.RootElement.GetProperty("requestId").GetString().ShouldBe(id.ToString());
    body.RootElement.GetProperty("right").GetString().ShouldBe("Erasure");
    body.RootElement.GetProperty("email").GetString().ShouldBe(email);
    body.RootElement.GetProperty("firstName").GetString().ShouldBe("Jeanne");
    body.RootElement.GetProperty("lastName").GetString().ShouldBe("Martin");
  }

  /// <summary>
  /// ⚠️ <b>Un prénom ou un nom absent part à <c>null</c></b> : les cinq clés sont toujours présentes,
  /// exactement comme sur le canal HTTP — un seul contrat pour l'intégrateur.
  /// </summary>
  [Fact]
  public async Task PublishesTheMissingNamesAsNull()
  {
    await BindAQueueAsync("droit.acces");
    await RouteAsync(DataSubjectRight.Access, "droit.acces");

    var (id, _) = await AnExecutableRequestAsync();
    (await _surface.ExecuteAsync(id)).StatusCode.ShouldBe(HttpStatusCode.OK);

    using var body = JsonDocument.Parse(Encoding.UTF8.GetString((await TheOnlyMessageInTheQueueAsync()).Body.Span));

    body.RootElement.GetProperty("firstName").ValueKind.ShouldBe(JsonValueKind.Null);
    body.RootElement.GetProperty("lastName").ValueKind.ShouldBe(JsonValueKind.Null);
  }

  /// <summary>
  /// <b>Le broker confirme, la demande passe à Terminée</b> : le 200 porte la ligne, au badge
  /// Terminée, le crayon éteint et l'exécution éteinte sous « Demande close ».
  /// </summary>
  [Fact]
  public async Task CompletesTheRequestWhenTheBrokerAcknowledges()
  {
    await BindAQueueAsync("droit.acces");
    await RouteAsync(DataSubjectRight.Access, "droit.acces");

    var (id, message) = await AnExecutableRequestAsync();

    var response = await _surface.ExecuteAsync(id);
    var row = await response.Content.ReadAsStringAsync();

    response.StatusCode.ShouldBe(HttpStatusCode.OK, row);
    row.ShouldContain(@"data-status=""Completed""", Case.Sensitive, "La ligne rendue n'est pas Terminée.");
    (await _surface.RowOfAsync(message))["status"].ShouldBe("Completed");
  }

  /// <summary>
  /// <b>La ligne de journal dit par où c'est parti</b> — l'exchange et la routing key en toutes
  /// lettres —, avec un <c>HttpStatus</c> nul, une <b>durée non nulle</b> qui court jusqu'à la
  /// confirmation, et <b>aucune donnée personnelle</b>.
  /// </summary>
  [Fact]
  public async Task LeavesOneAttemptSayingTheExchangeAndTheRoutingKeyWithoutPersonalData()
  {
    await BindAQueueAsync("droit.acces");
    await RouteAsync(DataSubjectRight.Access, "droit.acces");

    var (id, message) = await AnExecutableRequestAsync();
    var request = await _surface.RowOfAsync(message);

    var before = DateTimeOffset.UtcNow;
    (await _surface.ExecuteAsync(id)).StatusCode.ShouldBe(HttpStatusCode.OK);
    var after = DateTimeOffset.UtcNow;

    var attempt = (await _surface.AttemptsOfAsync(id))
      .ShouldHaveSingleItem("La publication n'a pas laissé une ligne de journal, une seule.");

    attempt["data_subject_right"].ShouldBe("Access");
    attempt["exercise"].ShouldBe($"exchange {_exchange}, routing key droit.acces");
    attempt["outcome"].ShouldBe("Succeeded");
    attempt["http_status"].ShouldBeNull("Une publication porte un statut HTTP.");
    attempt["created_by"].ShouldBe("operator");
    new DateTimeOffset(attempt["started_at"].ShouldBeOfType<DateTime>()).ShouldBeInRange(before, after);

    var duration = attempt["duration"].ShouldBeOfType<TimeSpan>();
    duration.ShouldBeGreaterThan(TimeSpan.Zero, "Une publication ne dure pas zéro au journal.");
    duration.ShouldBeLessThanOrEqualTo(after - before);

    var personal = new[] { request["email"], request["last_name"], request["first_name"], request["message"] }
      .OfType<string>();

    foreach (var value in attempt.Values.Select(value => value?.ToString() ?? string.Empty))
    {
      foreach (var datum in personal)
      {
        value.ShouldNotContain(datum, Case.Insensitive, "La ligne de journal porte une donnée personnelle.");
      }
    }
  }

  /// <summary>
  /// ⚠️ <b>Un message publié sur un exchange sans binding rend « non routable »</b> : le broker le
  /// rend au service, la demande reste <b>En cours</b>, et l'<c>Operator</c> lit que personne ne l'a
  /// reçu.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>C'est le test qui tient le drapeau <c>mandatory</c> en place</b> : sans lui, le message
  /// disparaîtrait en silence et la demande passerait à Terminée sur un mensonge (ADR-0028).
  /// </remarks>
  [Fact]
  public async Task RendersAnUnroutableMessageAndLeavesTheRequestInProgress()
  {
    // L'exchange existe — l'exploitant l'a déclaré —, mais aucune file ne lui est liée.
    await _topology.ExchangeDeclareAsync(_exchange, ExchangeType.Direct, durable: true, autoDelete: false);
    await RouteAsync(DataSubjectRight.Access, "droit.acces");

    var (id, message) = await AnExecutableRequestAsync();

    var response = await _surface.ExecuteAsync(id);

    await ShouldBeAProblemAsync(
      response, id, "Le message a été publié, mais aucune file ne l'a reçu. La demande reste En cours.");
    await ShouldStayInProgressWithOneAttemptAsync(id, message, "Unroutable");
  }

  /// <summary>
  /// ⚠️ <b>Le service ne déclare ni ne vérifie jamais l'exchange</b> : la topologie du bus reste la
  /// propriété de l'exploitant (ADR-0027). Publier sur un exchange qui n'existe pas ne le crée pas,
  /// et laisse la demande En cours.
  /// </summary>
  [Fact]
  public async Task DeclaresNothingOnTheBrokerAndLeavesTheRequestInProgress()
  {
    // Aucune déclaration : ni exchange, ni file, ni binding. Le Paramétrage, lui, l'accepte (ADR-0027).
    await RouteAsync(DataSubjectRight.Access, "droit.acces");

    var (id, message) = await AnExecutableRequestAsync();

    (await _surface.ExecuteAsync(id)).StatusCode.ShouldNotBe(HttpStatusCode.OK);

    (await _surface.RowOfAsync(message))["status"].ShouldBe("InProgress", "La demande a changé de statut.");

    await using var inspection = await _broker.CreateChannelAsync();
    await Should.ThrowAsync<OperationInterruptedException>(
      async () => await inspection.ExchangeDeclarePassiveAsync(_exchange),
      "Le service a déclaré l'exchange du Paramétrage.");
  }

  /// <summary>
  /// <b>Une republication après un échec est possible</b>, et <b>aucune</b> republication automatique
  /// n'a lieu : un échec laisse une ligne, une seule, et c'est le geste de l'<c>Operator</c> — une
  /// fois la file liée — qui fait aboutir la demande.
  /// </summary>
  [Fact]
  public async Task LetsTheOperatorPublishAgainAndNeverRepublishesOnItsOwn()
  {
    await _topology.ExchangeDeclareAsync(_exchange, ExchangeType.Direct, durable: true, autoDelete: false);
    await RouteAsync(DataSubjectRight.Access, "droit.acces");

    var (id, message) = await AnExecutableRequestAsync();

    (await _surface.ExecuteAsync(id)).StatusCode.ShouldBe(HttpStatusCode.BadGateway);
    await ShouldStayInProgressWithOneAttemptAsync(id, message, "Unroutable");

    // L'exploitant lie enfin sa file : rien n'a été republié dans l'intervalle.
    await BindAQueueAsync("droit.acces");

    (await _surface.ExecuteAsync(id)).StatusCode.ShouldBe(HttpStatusCode.OK);

    (await _surface.RowOfAsync(message))["status"].ShouldBe("Completed");
    (await _surface.AttemptsOfAsync(id)).Count.ShouldBe(2, "Le journal ne porte pas exactement les deux gestes.");
    (await TheOnlyMessageInTheQueueAsync()).BasicProperties.MessageId.ShouldBe(id.ToString());
  }

  /// <summary>
  /// <b>Un droit routé s'exécute comme un droit adressé</b> sur un déploiement qui déclare une
  /// connexion : le bouton est actif, et son infobulle dit son nom plutôt qu'un motif de blocage.
  /// </summary>
  [Fact]
  public async Task OffersTheExecutionOfARoutedRightWhenTheDeploymentCanPublish()
  {
    await RouteAsync(DataSubjectRight.Access, "droit.acces");
    var marker = Guid.NewGuid().ToString("N");

    await _surface.RecordAsync(new Dictionary<string, string>
    {
      ["email"] = $"jeanne.{marker}@example.org",
      ["identityVerified"] = "true",
    });

    var row = await _surface.BoardRowWithAsync(marker);

    ExecutionButtonIn(row).ShouldNotContain(
      "aria-disabled", Case.Sensitive, "L'exécution d'un droit routé est éteinte sur un déploiement qui sait publier.");
  }

  /// <summary>Le bouton d'exécution de la ligne, balise d'ouverture et contenu.</summary>
  private static string ExecutionButtonIn(string row) =>
    Regex.Matches(row, @"<button\b[^>]*>.*?</button>", RegexOptions.Singleline)
      .Select(button => button.Value)
      .Where(button => button.Contains(@"data-action=""execute""", StringComparison.Ordinal))
      .ShouldHaveSingleItem("La ligne ne porte pas son action « execute », une fois.");

  /// <summary>
  /// Déclare l'exchange de ce test, une file à lui, et le binding entre les deux — <b>la topologie
  /// que l'exploitant pose</b>, et que le service ne pose jamais.
  /// </summary>
  private async Task BindAQueueAsync(string routingKey)
  {
    await _topology.ExchangeDeclareAsync(_exchange, ExchangeType.Direct, durable: true, autoDelete: false);
    await _topology.QueueDeclareAsync(_queue, durable: true, exclusive: false, autoDelete: false);
    await _topology.QueueBindAsync(_queue, _exchange, routingKey);
  }

  /// <summary>Route le droit sur l'exchange de ce test, par le use case du Paramétrage.</summary>
  private Task RouteAsync(DataSubjectRight right, string routingKey) =>
    _surface.RouteAsync(right, new RabbitMqRouting(ExchangeName.From(_exchange), RoutingKey.From(routingKey)));

  /// <summary>
  /// Le message que la file porte, un seul — lu <b>sur le vrai broker</b>, avec ses propriétés telles
  /// qu'elles ont voyagé.
  /// </summary>
  private async Task<BasicGetResult> TheOnlyMessageInTheQueueAsync()
  {
    var received = await _topology.BasicGetAsync(_queue, autoAck: true);

    received.ShouldNotBeNull("La file liée au routage n'a rien reçu.");
    (await _topology.MessageCountAsync(_queue)).ShouldBe(0u, "La file a reçu plus d'un message.");

    return received;
  }

  /// <summary>Une demande En cours, à l'identité vérifiée, avec un email, qui invoque le droit d'accès.</summary>
  private Task<(Guid Id, string Message)> AnExecutableRequestAsync() =>
    _surface.RecordAsync(new Dictionary<string, string>
    {
      ["email"] = $"{Guid.NewGuid():N}@example.org",
      ["identityVerified"] = "true",
    });

  /// <summary>
  /// Une publication qui n'a pas abouti : <b>502</b>, un <c>ProblemDetails</c> dont <c>detail</c> est
  /// le texte, et <c>retryable</c> vrai — l'<c>Operator</c> peut recommencer.
  /// </summary>
  private async Task ShouldBeAProblemAsync(HttpResponseMessage response, Guid id, string reason)
  {
    var body = await response.Content.ReadAsStringAsync();

    response.StatusCode.ShouldBe(HttpStatusCode.BadGateway, body);
    response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

    using var document = JsonDocument.Parse(body);

    document.RootElement.GetProperty("detail").GetString().ShouldBe(reason);
    document.RootElement.GetProperty("retryable").GetBoolean()
      .ShouldBeTrue("Le ProblemDetails ne dit pas qu'une nouvelle tentative a un sens.");
    Regex.Match(document.RootElement.GetProperty("row").GetString()!, @"data-request-id=""([^""]*)""")
      .Groups[1].Value.ShouldBe(id.ToString());
  }

  /// <summary>
  /// La demande est toujours En cours, et le journal porte <b>une</b> tentative, au résultat attendu
  /// et sans statut HTTP.
  /// </summary>
  private async Task ShouldStayInProgressWithOneAttemptAsync(Guid id, string message, string outcome)
  {
    (await _surface.RowOfAsync(message))["status"].ShouldBe("InProgress", "La demande a changé de statut.");

    var attempt = (await _surface.AttemptsOfAsync(id))
      .ShouldHaveSingleItem("L'échec n'a pas laissé une ligne de journal, une seule.");

    attempt["outcome"].ShouldBe(outcome);
    attempt["http_status"].ShouldBeNull();
  }
}
