using System.Net;
using System.Text;
using System.Text.Json;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Casework.Adapters;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Casework;

/// <summary>
/// <c>read</c> tel qu'il part réellement sur le fil, et tel qu'il en revient : le droit dans le
/// corps, un flux d'octets en retour, et l'<b>enveloppe de transport</b> pour tout ce que le service
/// en saura.
/// </summary>
/// <remarks>
/// La doublure est posée <b>sur le fil</b>, comme pour <c>locate</c> : le <c>Content-Type</c> et le
/// <c>Content-Disposition</c> <b>sont</b> le contrat, et un port du domaine les aurait cachés
/// au-dessus de la couture — le code qui les lit serait alors resté non testé.
/// </remarks>
public class HttpAdapterReadsTests
{
  private const string Secret = "un-secret-partage";

  private static readonly Designation Email =
    Designation.Of(DesignationKind.Email, "helene.petit@example.fr");

  private static readonly byte[] SomeBytes = Encoding.UTF8.GetBytes("nom;commande\nHélène Petit;CMD-1");

  /// <summary>
  /// <b>Le corps porte le droit au titre duquel on lit</b>, sous son nom canonique anglais — et le
  /// sac, comme partout. Rien d'autre : ni forme attendue, ni type souhaité, ni version de schéma.
  /// </summary>
  [Fact]
  public async Task CarriesTheRightItReadsUnderAndNoExpectedShape()
  {
    var adapter = AdapterDouble.ServingAPiece(SomeBytes, "text/csv");

    await Calling(adapter).ReadAsync(ARead(), DataSubjectRight.Access);

    using var body = JsonDocument.Parse(adapter.LastBody!);

    body.RootElement.EnumerateObject().Select(field => field.Name)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ShouldBe(["designations", "right"]);

    body.RootElement.GetProperty("right").GetString().ShouldBe(nameof(DataSubjectRight.Access));
    body.RootElement.GetProperty("designations").GetArrayLength().ShouldBe(1);
  }

  /// <summary>
  /// L'appel emprunte la route de sa <see cref="Capability"/> et porte le secret en en-tête, comme
  /// les trois autres : <c>read</c> n'a pas de contrat de transport à lui.
  /// </summary>
  [Fact]
  public async Task TakesTheSameWireAsEveryOtherCapability()
  {
    var adapter = AdapterDouble.ServingAPiece(SomeBytes, "text/csv");

    await Calling(adapter).ReadAsync(ARead(), DataSubjectRight.Access);

    adapter.LastRequest.Method.ShouldBe(HttpMethod.Post);
    adapter.LastRequest.RequestUri!.AbsolutePath.ShouldBe("/rgpd/read");
    adapter.LastRequest.RequestUri!.Query.ShouldBe("?system_id=boutique");
    adapter.LastRequest.Headers.GetValues(AdapterWire.SecretHeader).ShouldBe([Secret]);
  }

  /// <summary>
  /// <b>Une pièce pleine</b> : les octets remontent tels quels, et l'enveloppe est <b>recopiée</b> —
  /// le nom réduit à son dernier segment, et rien du corps n'est ouvert pour en juger.
  /// </summary>
  [Fact]
  public async Task BringsBackThePieceAndItsEnvelopeWithoutOpeningIt()
  {
    var adapter = AdapterDouble.ServingAPiece(
      SomeBytes,
      "text/csv; charset=utf-8",
      "attachment; filename=\"/var/exports/brocanto-2026.csv\"");

    var answer = await Calling(adapter).ReadAsync(ARead(), DataSubjectRight.Access);

    answer.Outcome.ShouldBe(AdapterOutcome.Served);
    answer.Served!.Content.ShouldBe(SomeBytes);
    answer.Served.Envelope.ContentType.ShouldBe("text/csv; charset=utf-8");
    answer.Served.Envelope.FileName.ShouldBe("brocanto-2026.csv");
  }

  /// <summary>
  /// <b>Une pièce vide est une réponse, jamais une panne</b> — à la différence d'un <c>200</c> à
  /// <c>locate</c>, dont le corps vide ne sert rien. C'est elle qui rend l'incomplétude gratuite :
  /// dire « rien » ne coûte à l'<c>Adapter</c> aucun export d'une forme convenue.
  /// </summary>
  [Fact]
  public async Task TakesAnEmptyBodyForTheAnswerItIs()
  {
    var adapter = AdapterDouble.ServingAPiece([], "text/csv", "attachment; filename=\"vide.csv\"");

    var answer = await Calling(adapter).ReadAsync(ARead(), DataSubjectRight.Access);

    answer.Outcome.ShouldBe(AdapterOutcome.Served);
    answer.Served!.Content.ShouldBeEmpty();
    answer.Served.Envelope.FileName.ShouldBe("vide.csv");
  }

  /// <summary>
  /// <b>Sans en-tête, l'enveloppe dégrade proprement</b> : des octets sans type déclaré, et le nom
  /// sur le <c>system_id</c>. <c>send_file()</c> seul doit suffire, et ne rien écrire du tout ne
  /// doit pas perdre la pièce.
  /// </summary>
  [Fact]
  public async Task DegradesOntoTheSystemWhenTheAdapterNamesNothing()
  {
    var adapter = AdapterDouble.ServingAPiece(SomeBytes, contentType: null);

    var answer = await Calling(adapter).ReadAsync(ARead(), DataSubjectRight.Access);

    answer.Served!.Envelope.ContentType.ShouldBe(TransportEnvelope.UnnamedContentType);
    answer.Served.Envelope.FileName.ShouldBe("boutique");
  }

  /// <summary>
  /// <b>Le <c>Content-Type</c> arrive tel que l'<c>Adapter</c> l'a écrit</b>, y compris quand le
  /// transport ne sait pas le relire. « Recopié sans interprétation » se prend au mot : le remplacer
  /// par « octets sans type déclaré » cacherait à l'<c>Operator</c> ce que le client a réellement dit.
  /// </summary>
  [Fact]
  public async Task CopiesTheContentTypeAsTheAdapterWroteIt()
  {
    var adapter = AdapterDouble.ServingAPiece(SomeBytes, "csv, mais à notre façon");

    var answer = await Calling(adapter).ReadAsync(ARead(), DataSubjectRight.Access);

    answer.Served!.Envelope.ContentType.ShouldBe("csv, mais à notre façon");
    answer.Served.Content.ShouldBe(SomeBytes);
  }

  /// <summary>
  /// Un <c>Content-Disposition</c> que le transport ne sait pas relire ne perd pas la pièce : le nom
  /// dégrade sur le <c>system_id</c>, et les octets arrivent quand même.
  /// </summary>
  [Fact]
  public async Task KeepsThePieceWhenTheDispositionIsUnreadable()
  {
    var adapter = AdapterDouble.ServingAPiece(SomeBytes, "text/csv", "n'importe quoi ; ; ;");

    var answer = await Calling(adapter).ReadAsync(ARead(), DataSubjectRight.Access);

    answer.Served!.Content.ShouldBe(SomeBytes);
    answer.Served.Envelope.FileName.ShouldBe("boutique");
  }

  /// <summary>
  /// Un <c>202</c> sur <c>read</c> porte son <b>échéance déclarée</b>, exactement comme sur
  /// <c>locate</c> : le contrat de transport est un, et il ne se décline pas par capacité.
  /// </summary>
  [Fact]
  public async Task ReadsTheDeadlineADeferralDeclares()
  {
    var adapter = AdapterDouble.RespondingWith(
      HttpStatusCode.Accepted,
      "{\"deadline\":\"2026-08-05T09:00:00+02:00\"}");

    var answer = await Calling(adapter).ReadAsync(ARead(), DataSubjectRight.Access);

    answer.Outcome.ShouldBe(AdapterOutcome.Deferred);
    answer.DeclaredDeadline.ShouldBe(new DateTimeOffset(2026, 8, 5, 7, 0, 0, TimeSpan.Zero));
    answer.Served.ShouldBeNull();
    adapter.Asked.Count.ShouldBe(1);
  }

  /// <summary><b>Le service n'invente pas d'échéance</b>, pas plus ici qu'ailleurs.</summary>
  [Fact]
  public async Task RefusesToInventTheDeadlineADeferralDidNotDeclare()
  {
    var adapter = AdapterDouble.RespondingWith(HttpStatusCode.Accepted, "{}");

    await Should.ThrowAsync<AdapterFailure>(
      () => Calling(adapter).ReadAsync(ARead(), DataSubjectRight.Access));
  }

  /// <summary><b>Les deux refus ne se confondent pas</b>, et ils gardent leur distinction ici aussi.</summary>
  [Theory]
  [InlineData(HttpStatusCode.Unauthorized, nameof(AdapterOutcome.SecretRefused))]
  [InlineData(HttpStatusCode.NotFound, nameof(AdapterOutcome.SystemNotServed))]
  public async Task TellsTheTwoRefusalsApart(HttpStatusCode status, string expected)
  {
    var adapter = AdapterDouble.RespondingWith(status);

    var answer = await Calling(adapter).ReadAsync(ARead(), DataSubjectRight.Access);

    answer.Outcome.ShouldBe(AdapterOutcome.FromName(expected));
    answer.Served.ShouldBeNull();
  }

  /// <summary>
  /// Ce qui n'est <b>ni réponse ni refus</b> reste une panne : un <c>204</c> n'est pas une pièce
  /// vide — la pièce vide est un <c>200</c> sans octets, et le contrat n'a pas deux façons de le
  /// dire.
  /// </summary>
  [Theory]
  [InlineData(HttpStatusCode.InternalServerError)]
  [InlineData(HttpStatusCode.NoContent)]
  [InlineData(HttpStatusCode.Forbidden)]
  public async Task NamesAsAFailureWhatIsNeitherAnAnswerNorARefusal(HttpStatusCode status)
  {
    var adapter = AdapterDouble.RespondingWith(status);

    await Should.ThrowAsync<AdapterFailure>(
      () => Calling(adapter).ReadAsync(ARead(), DataSubjectRight.Access));
  }

  /// <summary>Le client de l'<c>Adapter</c>, branché sur la doublure posée sur le fil.</summary>
  private static HttpAdapterCalls Calling(AdapterDouble adapter)
  {
    return new HttpAdapterCalls(adapter.Client(), new AdapterSecret(Secret));
  }

  /// <summary>Un appel ordinaire : lire dans « la boutique ».</summary>
  private static AdapterCall ARead()
  {
    return new AdapterCall(
      AdapterAddress.From("https://brocanto.example.fr/rgpd"),
      DeclaredSystemId.From("boutique"),
      Capability.Read,
      [Email]);
  }
}
