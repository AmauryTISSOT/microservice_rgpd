using System.Net;
using System.Text.Json;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Infrastructure.Casework.Adapters;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Casework;

/// <summary>
/// Le contrat d'<c>Adapter</c> tel qu'il part réellement sur le fil, et tel qu'il en revient.
/// </summary>
/// <remarks>
/// Ces tests posent leur doublure <b>sur le fil</b> — un <c>HttpMessageHandler</c> injecté dans le
/// client — parce que l'en-tête de secret, le <c>system_id</c> en paramètre, le <c>202</c> et son
/// échéance <b>sont</b> le contrat : c'est ce que le développeur du client implémentera, et un port
/// du domaine les aurait cachés au-dessus de la couture.
/// </remarks>
public class HttpAdapterCallsTests
{
  private const string Secret = "un-secret-partage";

  private static readonly Designation Email =
    Designation.Of(DesignationKind.Email, "helene.petit@example.fr");

  private static readonly Designation Reference =
    Designation.Of(DesignationKind.Reference, "1203");

  /// <summary>
  /// Le secret part en <b>en-tête</b>, à chaque appel. Il ne voyage ni dans l'URL — où il serait
  /// journalisé par tout ce qui se trouve entre les deux — ni dans le corps.
  /// </summary>
  [Fact]
  public async Task CarriesTheSharedSecretInAHeaderAndNowhereElse()
  {
    var adapter = AdapterDouble.RespondingWith(HttpStatusCode.OK, "{\"count\":12}");

    await Calling(adapter).AskAsync<Found>(ALocate());

    adapter.LastRequest.Headers.GetValues(HttpAdapterCalls.SecretHeader).ShouldBe([Secret]);
    adapter.LastRequest.RequestUri!.ToString().ShouldNotContain(Secret);
    adapter.LastBody!.ShouldNotContain(Secret);
  }

  /// <summary>
  /// Le <c>system_id</c> voyage en <b>paramètre</b> de l'appel, jamais en corps : c'est ce qui
  /// permet à un seul <c>Adapter</c> de servir plusieurs systèmes.
  /// </summary>
  [Fact]
  public async Task PutsTheSystemInTheParametersRatherThanInTheBody()
  {
    var adapter = AdapterDouble.RespondingWith(HttpStatusCode.OK, "{\"count\":12}");

    await Calling(adapter).AskAsync<Found>(ALocate());

    var asked = adapter.LastRequest.RequestUri!;

    asked.AbsolutePath.ShouldBe("/rgpd/locate");
    asked.Query.ShouldBe("?system_id=boutique");
    adapter.LastBody!.ShouldNotContain("boutique");
  }

  /// <summary>
  /// <b>Une opération par <see cref="Capability"/></b>, nommée par le mot canonique du contrat — et
  /// non par le nom d'un membre C# qu'un renommage déplacerait sous les pieds du client.
  /// </summary>
  [Fact]
  public async Task NamesOneOperationPerCapability()
  {
    var adapter = AdapterDouble.RespondingWith(HttpStatusCode.OK, "{\"count\":0}");

    await Calling(adapter).AskAsync<Found>(ALocate() with { Capability = Capability.Read });

    adapter.LastRequest.Method.ShouldBe(HttpMethod.Post);
    adapter.LastRequest.RequestUri!.AbsolutePath.ShouldBe("/rgpd/read");
  }

  /// <summary>
  /// Le corps porte le <b>sac de désignations</b>, chacune par sa nature dans le mot du contrat.
  /// Rien d'autre n'y entre : ni identifiant de dossier, ni date, ni nom d'<c>Operator</c> — ce que
  /// l'<c>Adapter</c> ne reçoit pas ne peut pas finir dans ses journaux.
  /// </summary>
  [Fact]
  public async Task SendsTheBagOfDesignationsAndNothingOfTheCase()
  {
    var adapter = AdapterDouble.RespondingWith(HttpStatusCode.OK, "{\"count\":1}");

    await Calling(adapter).AskAsync<Found>(ALocate());

    using var body = JsonDocument.Parse(adapter.LastBody!);

    body.RootElement.EnumerateObject().Select(field => field.Name).ShouldBe(["designations"]);

    var designations = body.RootElement.GetProperty("designations");

    designations.GetArrayLength().ShouldBe(2);
    designations[0].GetProperty("kind").GetString().ShouldBe("email");
    designations[0].GetProperty("value").GetString().ShouldBe("helene.petit@example.fr");
    designations[1].GetProperty("kind").GetString().ShouldBe("reference");
  }

  /// <summary>Un <c>200</c> sert, et ce qu'il sert remonte tel que la capacité le décrit.</summary>
  [Fact]
  public async Task BringsBackWhatTheAdapterServed()
  {
    var adapter = AdapterDouble.RespondingWith(HttpStatusCode.OK, "{\"count\":12}");

    var answer = await Calling(adapter).AskAsync<Found>(ALocate());

    answer.Outcome.ShouldBe(AdapterOutcome.Served);
    answer.Served!.Count.ShouldBe(12);
    answer.DeclaredDeadline.ShouldBeNull();
  }

  /// <summary>
  /// Un <c>202</c> porte une <b>échéance déclarée</b> que le service sait relire, et
  /// <b>aucune connexion n'est tenue</b> : un seul aller, pas de sondage, pas de relance.
  /// </summary>
  [Fact]
  public async Task ReadsTheDeadlineADeferralDeclaresAndHoldsNoConnection()
  {
    var adapter = AdapterDouble.RespondingWith(
      HttpStatusCode.Accepted,
      "{\"deadline\":\"2026-08-05T09:00:00+02:00\"}");

    var answer = await Calling(adapter).AskAsync<Found>(ALocate());

    answer.Outcome.ShouldBe(AdapterOutcome.Deferred);
    answer.DeclaredDeadline.ShouldBe(new DateTimeOffset(2026, 8, 5, 7, 0, 0, TimeSpan.Zero));
    answer.Served.ShouldBeNull();
    adapter.Asked.Count.ShouldBe(1);
  }

  /// <summary>
  /// <b>Le service n'invente pas d'échéance.</b> Un différé qui n'en déclare aucune est une rupture
  /// du contrat : une date fabriquée ici deviendrait, une heure plus tard, une preuve que personne
  /// n'a déclarée.
  /// </summary>
  [Theory]
  [InlineData("{}")]
  [InlineData("{\"deadline\":\"bientôt\"}")]
  [InlineData("{\"deadline\":\"2026-08-05 09:00\"}")]
  public async Task RefusesToInventTheDeadlineADeferralDidNotDeclare(string body)
  {
    var adapter = AdapterDouble.RespondingWith(HttpStatusCode.Accepted, body);

    await Should.ThrowAsync<AdapterFailure>(() => Calling(adapter).AskAsync<Found>(ALocate()));
  }

  /// <summary>
  /// <b>Les deux refus ne se confondent pas.</b> Un secret invalide se répare dans la configuration
  /// de déploiement, des deux côtés à la fois ; un <c>system_id</c> non servi se répare dans le
  /// <c>Manifest</c> ou dans l'<c>Adapter</c>. Les fondre ferait chercher au mauvais endroit une
  /// fois sur deux.
  /// </summary>
  [Theory]
  [InlineData(HttpStatusCode.Unauthorized, nameof(AdapterOutcome.SecretRefused))]
  [InlineData(HttpStatusCode.NotFound, nameof(AdapterOutcome.SystemNotServed))]
  public async Task TellsTheTwoRefusalsApart(HttpStatusCode status, string expected)
  {
    var adapter = AdapterDouble.RespondingWith(status);

    var answer = await Calling(adapter).AskAsync<Found>(ALocate());

    answer.Outcome.ShouldBe(AdapterOutcome.FromName(expected));
    answer.Outcome.IsRefusal.ShouldBeTrue();
    answer.Served.ShouldBeNull();
    answer.DeclaredDeadline.ShouldBeNull();
  }

  /// <summary>
  /// Ce qui n'est <b>ni réponse ni refus</b> est une panne, et elle arrive nommée. La faire passer
  /// pour un refus ferait signaler un désaccord <c>Manifest</c>/<c>Adapter</c> là où il n'y a qu'un
  /// serveur en peine.
  /// </summary>
  [Theory]
  [InlineData(HttpStatusCode.InternalServerError)]
  [InlineData(HttpStatusCode.BadGateway)]
  [InlineData(HttpStatusCode.Forbidden)]
  [InlineData(HttpStatusCode.NoContent)]
  public async Task NamesAsAFailureWhatIsNeitherAnAnswerNorARefusal(HttpStatusCode status)
  {
    var adapter = AdapterDouble.RespondingWith(status);

    await Should.ThrowAsync<AdapterFailure>(() => Calling(adapter).AskAsync<Found>(ALocate()));
  }

  /// <summary>Un <c>200</c> illisible, ou vide, ne sert rien : c'est une panne, pas un résultat nul.</summary>
  [Theory]
  [InlineData("ce n'est pas du JSON")]
  [InlineData("null")]
  public async Task NamesAsAFailureABodyThatServesNothing(string body)
  {
    var adapter = AdapterDouble.RespondingWith(HttpStatusCode.OK, body);

    await Should.ThrowAsync<AdapterFailure>(() => Calling(adapter).AskAsync<Found>(ALocate()));
  }

  /// <summary>Le client de l'<c>Adapter</c>, branché sur la doublure posée sur le fil.</summary>
  private static HttpAdapterCalls Calling(AdapterDouble adapter)
  {
    return new HttpAdapterCalls(adapter.Client(), new AdapterSecret(Secret));
  }

  /// <summary>Un appel ordinaire : localiser une personne dans « la boutique ».</summary>
  private static AdapterCall ALocate()
  {
    return new AdapterCall(
      AdapterAddress.From("https://brocanto.example.fr/rgpd"),
      DeclaredSystemId.From("boutique"),
      Capability.Locate,
      [Email, Reference]);
  }

  /// <summary>
  /// Ce qu'une capacité rend, du seul point de vue du transport. Le contrat de <c>Locate</c> se
  /// décide ailleurs ; ce type est là pour prouver que celui-ci est <b>indifférent</b> à ce que la
  /// capacité rend.
  /// </summary>
  private sealed record Found(int Count);
}
