using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.TestDoubles.HostSystem;
using MicroserviceRgpd.UseCases.Configuration.SetRightEndpoint;
using MicroserviceRgpd.UseCases.Configuration.SetRightRabbitMqRouting;
using MicroserviceRgpd.Web.Pages.Requests;
using Microsoft.EntityFrameworkCore;
using NSwag.Generation;

namespace MicroserviceRgpd.FunctionalTests.Requests;

/// <summary>
/// <b>Exécuter une demande</b>, exercé par la <b>seule frontière HTTP</b> : le handler
/// <c>POST /demandes?handler=Execute</c>, frappé directement, jeton anti-rejeu compris — et un système
/// hôte factice qui écoute sur un port réel, à l'adresse posée dans le Paramétrage (ADR-0026).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Rien n'est remplacé dans le conteneur</b> : c'est le vrai client HTTP du service qui appelle le
/// système hôte factice, et ce que celui-ci reçoit est ce qu'un intégrateur recevrait.
/// </para>
/// <para>
/// ⚠️ <b>Chaque test part d'un Paramétrage vierge et d'un hôte qui n'a rien reçu</b>, et les rend tels
/// en sortant : le Paramétrage est un singleton, et la base est partagée par toute la collection.
/// </para>
/// </remarks>
[Collection(RequestExecutionWebCollection.Name)]
public class RequestExecution(CustomWebApplicationFactory<Program> factory) : IAsyncLifetime
{
  private readonly RequestSurface _surface = new(factory);

  private HostSystemDouble _host = null!;

  public async Task InitializeAsync()
  {
    await ForgetEveryEndpointAsync();
    _host = await HostSystemDouble.StartAsync();
  }

  public async Task DisposeAsync()
  {
    await _host.DisposeAsync();
    await ForgetEveryEndpointAsync();
  }

  /// <summary>
  /// <b>Le système hôte reçoit un <c>POST</c> <c>application/json</c></b>, à l'adresse du Paramétrage
  /// telle quelle — query string comprise —, sans en-tête d'authentification, dont le corps porte
  /// <b>exactement</b> les cinq clés : l'identifiant en texte et le droit sous son nom canonique.
  /// </summary>
  [Fact]
  public async Task SendsAJsonPostWithExactlyTheFiveKeysToTheEndpointOfTheRight()
  {
    await ConfigureAsync(DataSubjectRight.Erasure, _host.AddressOf("/rights/erasure?tenant=brocanto"));
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

    var received = _host.Received.ShouldHaveSingleItem("Le système hôte n'a pas reçu un appel, un seul.");

    received.Method.ShouldBe("POST");
    received.PathAndQuery.ShouldBe("/rights/erasure?tenant=brocanto");
    received.ContentType.ShouldNotBeNull().Split(';')[0].ShouldBe("application/json");
    received.Authorization.ShouldBeEmpty("L'appel porte un en-tête d'authentification.");

    using var body = JsonDocument.Parse(received.Body);

    body.RootElement.EnumerateObject().Select(property => property.Name)
      .ShouldBe(["requestId", "right", "email", "firstName", "lastName"], ignoreOrder: true);
    body.RootElement.GetProperty("requestId").GetString().ShouldBe(id.ToString());
    body.RootElement.GetProperty("right").GetString().ShouldBe("Erasure");
    body.RootElement.GetProperty("email").GetString().ShouldBe(email);
    body.RootElement.GetProperty("firstName").GetString().ShouldBe("Jeanne");
    body.RootElement.GetProperty("lastName").GetString().ShouldBe("Martin");
  }

  /// <summary>
  /// ⚠️ <b>Un prénom ou un nom absent part à <c>null</c></b> : les cinq clés sont toujours présentes.
  /// </summary>
  [Fact]
  public async Task SendsTheMissingNamesAsNull()
  {
    await ConfigureAsync(DataSubjectRight.Access, _host.AddressOf("/rights/access"));

    var (id, _) = await _surface.RecordAsync(new Dictionary<string, string>
    {
      ["email"] = $"{Guid.NewGuid():N}@example.org",
      ["identityVerified"] = "true",
    });

    (await _surface.ExecuteAsync(id)).StatusCode.ShouldBe(HttpStatusCode.OK);

    using var body = JsonDocument.Parse(_host.Received.ShouldHaveSingleItem().Body);

    body.RootElement.GetProperty("firstName").ValueKind.ShouldBe(JsonValueKind.Null);
    body.RootElement.GetProperty("lastName").ValueKind.ShouldBe(JsonValueKind.Null);
  }

  /// <summary>
  /// <b>Tout 2xx fait passer la demande à Terminée</b> — <c>202 Accepted</c> compris : le 200 porte la
  /// ligne, au badge Terminée, le crayon éteint et l'exécution éteinte sous « Demande close ».
  /// </summary>
  [Theory]
  [InlineData(200)]
  [InlineData(202)]
  [InlineData(204)]
  public async Task CompletesTheRequestOnEvery2xxAndRendersItsRow(int statusCode)
  {
    await ConfigureAsync(DataSubjectRight.Access, _host.AddressOf("/rights/access"));
    _host.Answer(statusCode);

    var (id, message) = await AnExecutableRequestAsync();

    var response = await _surface.ExecuteAsync(id);
    var row = await response.Content.ReadAsStringAsync();

    response.StatusCode.ShouldBe(HttpStatusCode.OK, row);
    response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("text/html");

    Regex.Matches(row, @"<tr\b").Count.ShouldBe(1, "Le 200 ne porte pas une ligne, une seule.");
    Regex.Match(row, @"data-request-id=""([^""]*)""").Groups[1].Value.ShouldBe(id.ToString());
    row.ShouldContain(@"data-status=""Completed""", Case.Sensitive, "La ligne rendue n'est pas Terminée.");
    ActionIn(row, "edit").ShouldContain(@"aria-disabled=""true""", Case.Sensitive, "Le crayon d'une demande Terminée est allumé.");
    ActionIn(row, "execute").ShouldContain(
      ExecutionBlock.Closed.FrenchLabelFor(DataSubjectRight.Access), Case.Sensitive, "L'exécution d'une demande Terminée ne dit pas « Demande close ».");

    (await _surface.RowOfAsync(message))["status"].ShouldBe("Completed");
  }

  /// <summary>
  /// <b>Chaque appel parti laisse une ligne de journal</b> : la demande, le droit, l'adresse <b>sans
  /// query string ni fragment</b>, l'instant de début, la durée, le résultat, le statut HTTP et
  /// l'auteur — et <b>aucune donnée personnelle</b>.
  /// </summary>
  [Fact]
  public async Task LeavesOneAttemptWithoutPersonalDataInTheExecutionLog()
  {
    await ConfigureAsync(DataSubjectRight.Access, _host.AddressOf("/rights/access?token=secret"));
    _host.Answer(204);

    var (id, message) = await AnExecutableRequestAsync();
    var request = await _surface.RowOfAsync(message);

    var before = DateTimeOffset.UtcNow;
    (await _surface.ExecuteAsync(id)).StatusCode.ShouldBe(HttpStatusCode.OK);
    var after = DateTimeOffset.UtcNow;

    var attempt = (await _surface.AttemptsOfAsync(id)).ShouldHaveSingleItem("L'exécution n'a pas laissé une ligne de journal, une seule.");

    attempt.Keys.ShouldBe(
      ["id", "data_subject_request_id", "data_subject_right", "exercise", "started_at", "duration", "outcome", "http_status", "created_by"],
      ignoreOrder: true);

    attempt["data_subject_request_id"].ShouldBe(id);
    attempt["data_subject_right"].ShouldBe("Access");
    attempt["exercise"].ShouldBe(_host.AddressOf("/rights/access"));
    new DateTimeOffset(attempt["started_at"].ShouldBeOfType<DateTime>()).ShouldBeInRange(before, after);
    attempt["duration"].ShouldBeOfType<TimeSpan>().ShouldBeInRange(TimeSpan.Zero, after - before);
    attempt["outcome"].ShouldBe("Succeeded");
    attempt["http_status"].ShouldBe(204);
    attempt["created_by"].ShouldBe("operator");

    var personal = new[] { request["email"], request["last_name"], request["first_name"], request["message"] }.OfType<string>();

    foreach (var value in attempt.Values.Select(value => value?.ToString() ?? string.Empty))
    {
      foreach (var datum in personal)
      {
        value.ShouldNotContain(datum, Case.Insensitive, "La ligne de journal porte une donnée personnelle.");
      }
    }
  }

  /// <summary>
  /// ⚠️ <b>Le journal survit à la suppression de la demande</b> : sa ligne garde un identifiant qui ne
  /// mène plus à personne, et prouve qu'un droit a été demandé au système hôte.
  /// </summary>
  [Fact]
  public async Task KeepsTheAttemptWhenTheRequestIsDeleted()
  {
    await ConfigureAsync(DataSubjectRight.Access, _host.AddressOf("/rights/access"));
    var (id, message) = await AnExecutableRequestAsync();

    (await _surface.ExecuteAsync(id)).StatusCode.ShouldBe(HttpStatusCode.OK);
    (await _surface.DeleteAsync(id)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

    (await _surface.CountOfAsync(message)).ShouldBe(0);
    (await _surface.AttemptsOfAsync(id)).ShouldHaveSingleItem("La suppression de la demande a emporté son journal.");
  }

  /// <summary>
  /// <b>Un système hôte qui répond autre chose qu'un 2xx rend 502</b> : le <c>ProblemDetails</c> dit le
  /// code et que la demande reste En cours, porte la ligne inchangée, et le journal retient la
  /// tentative avec son statut.
  /// </summary>
  [Theory]
  [InlineData(400)]
  [InlineData(404)]
  [InlineData(500)]
  [InlineData(503)]
  public async Task AnswersBadGatewayAndLeavesTheRequestInProgressOnANon2xx(int statusCode)
  {
    await ConfigureAsync(DataSubjectRight.Access, _host.AddressOf("/rights/access"));
    _host.Answer(statusCode);
    var (id, message) = await AnExecutableRequestAsync();

    var response = await _surface.ExecuteAsync(id);

    await ShouldBeAProblemAsync(
      response, HttpStatusCode.BadGateway, id, $"Le système hôte a répondu {statusCode}. La demande reste En cours.");
    _host.Received.ShouldHaveSingleItem("Le système hôte a été rappelé : une nouvelle tentative est partie.");
    await ShouldStayInProgressWithOneAttemptAsync(id, message, "NonSuccessResponse", statusCode);
  }

  /// <summary>
  /// ⚠️ <b>Une redirection n'est pas suivie</b> : elle compte comme une réponse non 2xx, et l'hôte vers
  /// lequel elle renvoie ne reçoit rien — les données ne partent pas vers une adresse que le
  /// Paramétrage ne nomme pas.
  /// </summary>
  [Fact]
  public async Task DoesNotFollowARedirectAndCountsItAsANon2xx()
  {
    await using var elsewhere = await HostSystemDouble.StartAsync();
    await ConfigureAsync(DataSubjectRight.Access, _host.AddressOf("/rights/access"));
    _host.RedirectTo(elsewhere.AddressOf("/rights/access"));
    var (id, message) = await AnExecutableRequestAsync();

    var response = await _surface.ExecuteAsync(id);

    await ShouldBeAProblemAsync(
      response, HttpStatusCode.BadGateway, id, "Le système hôte a répondu 302. La demande reste En cours.");
    elsewhere.Received.ShouldBeEmpty("La redirection a été suivie.");
    _host.Received.ShouldHaveSingleItem();
    await ShouldStayInProgressWithOneAttemptAsync(id, message, "NonSuccessResponse", 302);
  }

  /// <summary>
  /// <b>Un système hôte qui ne répond pas dans le délai rend 502</b>, qui dit ce délai — réduit en test
  /// par <c>HostSystem:TimeoutSeconds</c> —, et le journal retient un délai dépassé, sans statut.
  /// </summary>
  [Fact]
  public async Task AnswersBadGatewayAndLeavesTheRequestInProgressWhenTheHostDoesNotAnswerInTime()
  {
    await ConfigureAsync(DataSubjectRight.Access, _host.AddressOf("/rights/access"));
    _host.Delay(TimeSpan.FromSeconds(CustomWebApplicationFactory<Program>.HostSystemTimeoutSeconds + 3));
    var (id, message) = await AnExecutableRequestAsync();

    var response = await _surface.ExecuteAsync(id);

    await ShouldBeAProblemAsync(
      response,
      HttpStatusCode.BadGateway,
      id,
      $"Le système hôte n'a pas répondu dans les {CustomWebApplicationFactory<Program>.HostSystemTimeoutSeconds} secondes. La demande reste En cours.");
    _host.Received.ShouldHaveSingleItem("Le système hôte a été rappelé : une nouvelle tentative est partie.");
    await ShouldStayInProgressWithOneAttemptAsync(id, message, "TimedOut", null);
  }

  /// <summary>
  /// <b>Une demande close rend 409</b>, sans appel ni ligne de journal : le <c>ProblemDetails</c> dit
  /// « Demande close » et porte la ligne à jour.
  /// </summary>
  [Fact]
  public async Task AnswersConflictForAClosedRequestWithoutCallingNorLogging()
  {
    await ConfigureAsync(DataSubjectRight.Access, _host.AddressOf("/rights/access"));
    var (id, _) = await AnExecutableRequestAsync();
    await _surface.SetStatusAsync(id, nameof(RequestStatus.Completed));

    var response = await _surface.ExecuteAsync(id);

    await ShouldBeAProblemAsync(response, HttpStatusCode.Conflict, id, ExecutionBlock.Closed.FrenchLabelFor(DataSubjectRight.Access));
    _host.Received.ShouldBeEmpty("Une demande close a été envoyée au système hôte.");
    (await _surface.AttemptsOfAsync(id)).ShouldBeEmpty("Un refus a écrit une ligne de journal.");
  }

  /// <summary>
  /// <b>Tout autre motif rend 422</b>, sans appel ni ligne de journal : le <c>ProblemDetails</c> dit le
  /// motif et porte la ligne à jour.
  /// </summary>
  [Theory]
  [InlineData(nameof(ExecutionBlock.IdentityNotVerified))]
  [InlineData(nameof(ExecutionBlock.EmailMissing))]
  [InlineData(nameof(ExecutionBlock.RightNotConfigured))]
  [InlineData(nameof(ExecutionBlock.BrokerConnectionMissing))]
  public async Task AnswersUnprocessableForEveryOtherBlockWithoutCallingNorLogging(string blockName)
  {
    var block = ExecutionBlock.FromName(blockName);

    if (block == ExecutionBlock.BrokerConnectionMissing)
    {
      await RouteAsync(DataSubjectRight.Access);
    }
    else if (block != ExecutionBlock.RightNotConfigured)
    {
      await ConfigureAsync(DataSubjectRight.Access, _host.AddressOf("/rights/access"));
    }

    var (id, _) = await _surface.RecordAsync(new Dictionary<string, string>
    {
      ["lastName"] = "Martin",
      ["firstName"] = "Jeanne",
      ["email"] = block == ExecutionBlock.EmailMissing ? "" : $"{Guid.NewGuid():N}@example.org",
      ["identityVerified"] = block == ExecutionBlock.IdentityNotVerified ? "false" : "true",
    });

    var response = await _surface.ExecuteAsync(id);

    await ShouldBeAProblemAsync(response, HttpStatusCode.UnprocessableEntity, id, block.FrenchLabelFor(DataSubjectRight.Access));
    _host.Received.ShouldBeEmpty("Une demande bloquée a été envoyée au système hôte.");
    (await _surface.AttemptsOfAsync(id)).ShouldBeEmpty("Un refus a écrit une ligne de journal.");
  }

  /// <summary><b>Une demande disparue rend 404</b>, sans appel.</summary>
  [Fact]
  public async Task AnswersNotFoundForARequestThatDoesNotExist()
  {
    await ConfigureAsync(DataSubjectRight.Access, _host.AddressOf("/rights/access"));
    var (id, _) = await AnExecutableRequestAsync();
    (await _surface.DeleteAsync(id)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

    (await _surface.ExecuteAsync(id)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    _host.Received.ShouldBeEmpty();
  }

  /// <summary><b>Un identifiant qui n'en est pas un rend 400</b>, et non 404.</summary>
  [Theory]
  [InlineData("")]
  [InlineData("pas-un-guid")]
  [InlineData("00000000-0000-0000-0000-000000000000")]
  public async Task RefusesAnIdThatIsNotOne(string id)
  {
    (await _surface.ExecuteAsync(id)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    _host.Received.ShouldBeEmpty();
  }

  /// <summary>⚠️ <b>Sans jeton anti-rejeu, rien ne part</b> : une page tierce n'atteint pas le handler.</summary>
  [Fact]
  public async Task ExecutesNothingWithoutTheAntiforgeryToken()
  {
    await ConfigureAsync(DataSubjectRight.Access, _host.AddressOf("/rights/access"));
    var (id, message) = await AnExecutableRequestAsync();

    (await _surface.ExecuteWithoutTokenAsync(id)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

    _host.Received.ShouldBeEmpty();
    (await _surface.RowOfAsync(message))["status"].ShouldBe("InProgress");
  }

  /// <summary>⚠️ <b>L'exécution n'est pas une API</b> : le document Swagger n'en publie rien.</summary>
  [Fact]
  public async Task TheApiDocumentDoesNotPublishTheExecution()
  {
    using var scope = factory.Services.CreateScope();
    var document = await scope.ServiceProvider.GetRequiredService<IOpenApiDocumentGenerator>().GenerateAsync("v1");
    var published = document.ToJson();

    document.Paths.Keys.ShouldContain("/qualifications", "Le document ne publie plus rien : les assertions suivantes seraient vides.");
    published.ShouldNotContain("handler=Execute", Case.Insensitive, "Le document Swagger publie l'exécution d'une demande.");
    published.ShouldNotContain("ExecuteDataSubjectRequest", Case.Insensitive, "Le document Swagger publie l'exécution d'une demande.");
    published.ShouldNotContain("handler=Execution", Case.Insensitive, "Le document Swagger publie le récapitulatif d'une exécution.");
    published.ShouldNotContain("ExecutionSummary", Case.Insensitive, "Le document Swagger publie le récapitulatif d'une exécution.");
  }

  /// <summary>
  /// <b>Le récapitulatif dit ce que le système hôte recevra et à quelle adresse</b> — le droit avec son
  /// article, le prénom, le nom, l'email et l'adresse appelée, query string comprise —, relu sur le
  /// serveur, en JSON. Une demande exécutable n'a pas de motif de blocage, et le système hôte n'est pas
  /// appelé.
  /// </summary>
  [Fact]
  public async Task AnswersTheSummaryOfWhatTheHostSystemWillReceiveAndWhere()
  {
    var address = _host.AddressOf("/rights/erasure?tenant=brocanto");
    await ConfigureAsync(DataSubjectRight.Erasure, address);
    var email = $"{Guid.NewGuid():N}@example.org";

    var (id, _) = await _surface.RecordAsync(new Dictionary<string, string>
    {
      ["email"] = email,
      ["lastName"] = "Martin",
      ["firstName"] = "Jeanne",
      ["identityVerified"] = "true",
      ["right"] = nameof(DataSubjectRight.Erasure),
    });

    var summary = await SummaryOfAsync(id);

    summary.Keys.ShouldBe(["right", "firstName", "lastName", "email", "exercise", "warning", "block"], ignoreOrder: true);
    summary["right"].GetString().ShouldBe("Droit à l'effacement (art. 17)");
    summary["firstName"].GetString().ShouldBe("Jeanne");
    summary["lastName"].GetString().ShouldBe("Martin");
    summary["email"].GetString().ShouldBe(email);
    summary["exercise"].GetString().ShouldBe(address);
    summary["warning"].GetString().ShouldBe(ExecutionConfirmation.AddressedWarning);
    summary["block"].ValueKind.ShouldBe(JsonValueKind.Null, "Une demande exécutable porte un motif de blocage.");
    _host.Received.ShouldBeEmpty("Lire le récapitulatif a appelé le système hôte.");
  }

  /// <summary>
  /// <b>Une valeur absente se dit « — »</b>, écrit par le serveur : un prénom, un nom, ou l'exercice
  /// d'un droit « non configuré ».
  /// </summary>
  [Fact]
  public async Task AnswersADashForEveryMissingValue()
  {
    var (id, _) = await AnExecutableRequestAsync();

    var summary = await SummaryOfAsync(id);

    summary["firstName"].GetString().ShouldBe("—");
    summary["lastName"].GetString().ShouldBe("—");
    summary["exercise"].GetString().ShouldBe("—");

    // Un droit « non configuré » garde la phrase de l'adresse — voir ExecutionConfirmation.WarningOf.
    summary["warning"].GetString().ShouldBe(ExecutionConfirmation.AddressedWarning);
  }

  /// <summary>
  /// <b>Une demande devenue non exécutable le dit dès le récapitulatif</b>, avec le premier motif de
  /// blocage, relu à l'instant sur la demande et le Paramétrage.
  /// </summary>
  [Theory]
  [InlineData(nameof(ExecutionBlock.Closed))]
  [InlineData(nameof(ExecutionBlock.RightNotConfigured))]
  [InlineData(nameof(ExecutionBlock.BrokerConnectionMissing))]
  public async Task AnswersTheBlockOfARequestThatNoLongerExecutes(string blockName)
  {
    var block = ExecutionBlock.FromName(blockName);

    if (block == ExecutionBlock.BrokerConnectionMissing)
    {
      await RouteAsync(DataSubjectRight.Access);
    }
    else if (block != ExecutionBlock.RightNotConfigured)
    {
      await ConfigureAsync(DataSubjectRight.Access, _host.AddressOf("/rights/access"));
    }

    var (id, _) = await AnExecutableRequestAsync();

    if (block == ExecutionBlock.Closed)
    {
      await _surface.SetStatusAsync(id, nameof(RequestStatus.Completed));
    }

    (await SummaryOfAsync(id))["block"].GetString().ShouldBe(block.FrenchLabelFor(DataSubjectRight.Access));
  }

  /// <summary>
  /// ⚠️ <b>Rien n'est gardé</b> : le récapitulatif se relit à chaque ouverture de la modale, et un cache
  /// de navigateur rendrait une adresse ou des données périmées.
  /// </summary>
  [Fact]
  public async Task KeepsTheSummaryOutOfEveryCache()
  {
    var (id, _) = await AnExecutableRequestAsync();

    var response = await _surface.ExecutionOfAsync(id);

    response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    response.Headers.CacheControl
      .ShouldNotBeNull("La réponse ne dit rien de sa mise en cache.")
      .NoStore.ShouldBeTrue("Le récapitulatif d'une exécution est mis en cache.");
  }

  /// <summary><b>Le récapitulatif d'une demande disparue rend 404.</b></summary>
  [Fact]
  public async Task AnswersNotFoundForTheSummaryOfARequestThatDoesNotExist()
  {
    var (id, _) = await AnExecutableRequestAsync();
    (await _surface.DeleteAsync(id)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

    (await _surface.ExecutionOfAsync(id)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  /// <summary><b>Le récapitulatif sous un identifiant qui n'en est pas un rend 400</b>, et non 404.</summary>
  [Theory]
  [InlineData("")]
  [InlineData("pas-un-guid")]
  [InlineData("00000000-0000-0000-0000-000000000000")]
  public async Task RefusesTheSummaryOfAnIdThatIsNotOne(string id)
  {
    (await _surface.ExecutionOfAsync(id)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
  }

  /// <summary>Le récapitulatif de la demande, champ par champ, la réponse ayant d'abord été reconnue 200 JSON.</summary>
  private async Task<IReadOnlyDictionary<string, JsonElement>> SummaryOfAsync(Guid id)
  {
    var response = await _surface.ExecutionOfAsync(id);

    response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");

    using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    return body.RootElement.EnumerateObject().ToDictionary(field => field.Name, field => field.Value.Clone());
  }

  /// <summary>Une demande En cours, à l'identité vérifiée, avec un email, qui invoque le droit d'accès.</summary>
  private Task<(Guid Id, string Message)> AnExecutableRequestAsync() =>
    _surface.RecordAsync(new Dictionary<string, string>
    {
      ["email"] = $"{Guid.NewGuid():N}@example.org",
      ["identityVerified"] = "true",
    });

  /// <summary>
  /// Un refus ou un échec d'exécution : le code, un <c>ProblemDetails</c> dont <c>detail</c> est le texte, et la
  /// ligne à jour de la demande sous <c>row</c> — celle même que le tableau rend —, et <c>retryable</c> :
  /// vrai pour un appel qui n'a pas abouti (502), faux pour un motif de blocage.
  /// </summary>
  private async Task ShouldBeAProblemAsync(HttpResponseMessage response, HttpStatusCode status, Guid id, string reason)
  {
    var body = await response.Content.ReadAsStringAsync();

    response.StatusCode.ShouldBe(status, body);
    response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

    using var document = JsonDocument.Parse(body);

    document.RootElement.GetProperty("status").GetInt32().ShouldBe((int)status);
    document.RootElement.GetProperty("detail").GetString().ShouldBe(reason);
    document.RootElement.GetProperty("retryable").GetBoolean()
      .ShouldBe(status == HttpStatusCode.BadGateway, "Le ProblemDetails ne dit pas si une nouvelle tentative a un sens.");

    var row = document.RootElement.GetProperty("row").GetString().ShouldNotBeNull().Trim();

    Regex.Match(row, @"data-request-id=""([^""]*)""").Groups[1].Value.ShouldBe(id.ToString());
    row.ShouldBe(await _surface.BoardRowWithAsync(id.ToString()), "La ligne du refus n'est pas celle que le tableau rend.");
  }

  /// <summary>
  /// La demande est toujours En cours, et le journal porte <b>une</b> tentative, au résultat et au statut
  /// HTTP attendus.
  /// </summary>
  private async Task ShouldStayInProgressWithOneAttemptAsync(Guid id, string message, string outcome, int? httpStatus)
  {
    (await _surface.RowOfAsync(message))["status"].ShouldBe("InProgress", "La demande a changé de statut.");

    var attempt = (await _surface.AttemptsOfAsync(id)).ShouldHaveSingleItem("L'échec n'a pas laissé une ligne de journal, une seule.");

    attempt["outcome"].ShouldBe(outcome);
    attempt["http_status"].ShouldBe(httpStatus);
  }

  /// <summary>La balise d'ouverture et le contenu du bouton d'action <paramref name="action"/> de la ligne.</summary>
  private static string ActionIn(string row, string action) =>
    Regex.Matches(row, @"<button\b[^>]*>.*?</button>", RegexOptions.Singleline)
      .Select(button => button.Value)
      .Where(button => button.Contains($@"data-action=""{action}""", StringComparison.Ordinal))
      .ShouldHaveSingleItem($"La ligne ne porte pas son action « {action} », une fois.");

  /// <summary>Pose l'adresse du droit, par le use case du Paramétrage.</summary>
  private async Task ConfigureAsync(DataSubjectRight right, string address)
  {
    using var scope = factory.Services.CreateScope();

    var set = await scope.ServiceProvider.GetRequiredService<Mediator.IMediator>().Send(
      new SetRightEndpointCommand(right, EndpointUrl.From(address)));

    set.IsSuccess.ShouldBeTrue();
  }

  /// <summary>
  /// <b>Le récapitulatif d'un droit exercé par RabbitMQ dit le routage et avertit du bus</b> —
  /// l'exchange et la routing key sous le champ « Exercice », puis la phrase qui dit que le service
  /// saura que le broker a accepté le message, <b>jamais</b> que le système hôte l'a traité —, et
  /// l'appel au système hôte n'a pas lieu. ⚠️ Ce déploiement ne déclare aucune connexion : le motif le
  /// dit (ADR-0028).
  /// </summary>
  [Fact]
  public async Task AnswersTheRoutingOfARightExercisedByRabbitMq()
  {
    await RouteAsync(DataSubjectRight.Access);

    var (id, _) = await AnExecutableRequestAsync();

    var summary = await SummaryOfAsync(id);

    summary["exercise"].GetString().ShouldBe("exchange rgpd.exercice, routing key droit.acces");
    summary["warning"].GetString().ShouldBe(ExecutionConfirmation.RoutedWarning);
    summary["block"].GetString()
      .ShouldBe(ExecutionBlock.BrokerConnectionMissing.FrenchLabelFor(DataSubjectRight.Access));
    _host.Received.ShouldBeEmpty("Lire le récapitulatif a appelé le système hôte.");
  }

  /// <summary>Route le droit sur RabbitMQ, par le use case du Paramétrage.</summary>
  private Task RouteAsync(DataSubjectRight right) =>
    _surface.RouteAsync(right, new RabbitMqRouting(ExchangeName.From("rgpd.exercice"), RoutingKey.From("droit.acces")));

  /// <summary>Ramène le service à son état d'installation : aucune ligne de Paramétrage.</summary>
  private Task ForgetEveryEndpointAsync() => _surface.ForgetEveryChannelAsync();
}
