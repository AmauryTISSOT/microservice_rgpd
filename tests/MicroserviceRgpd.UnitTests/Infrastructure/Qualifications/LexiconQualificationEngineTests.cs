using System.Net;
using System.Text.Json;
using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.Infrastructure.Qualifications;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Qualifications;

/// <summary>
/// L'adaptateur qui fait du sidecar lexical un moteur du domaine. Il se teste <b>sans réseau</b> :
/// un <see cref="HttpMessageHandler"/> double tient lieu de sidecar, et aucun test de la suite .NET
/// n'a jamais besoin qu'un processus Python tourne.
/// <para>
/// Ce qu'il vérifie est la promesse du port : <b>un avis, ou rien</b>. Tout ce qui n'est pas un
/// avis satisfaisant les invariants du domaine ressort en panne nommée, jamais en verdict boiteux.
/// </para>
/// </summary>
public class LexiconQualificationEngineTests
{
  private static readonly RightsRequestText Text =
    RightsRequestText.From("Supprimez toutes les données que vous avez sur moi.");

  [Fact]
  public async Task AsksTheLexiconEndpointForTheTextAndNothingElse()
  {
    var sidecar = RespondingWith("""{"rights":["Erasure"],"engine":{"name":"lexicon","version":"1.0.0"}}""");

    await Engine(sidecar).QualifyAsync(Text, CancellationToken.None);

    sidecar.LastRequest!.Method.ShouldBe(HttpMethod.Post);
    sidecar.LastRequest.RequestUri!.AbsolutePath.ShouldBe("/opinions/lexicon");

    using var body = JsonDocument.Parse(sidecar.LastRequestBody!);
    body.RootElement.GetProperty("text").GetString().ShouldBe(Text.Value);
    body.RootElement.EnumerateObject().Count().ShouldBe(1);
  }

  [Fact]
  public async Task ReadsTheCanonicalNamesBackIntoTheTaxonomy()
  {
    var sidecar = RespondingWith("""{"rights":["Access","Erasure"],"engine":{"name":"lexicon","version":"1.0.0"}}""");

    var opinion = await Engine(sidecar).QualifyAsync(Text, CancellationToken.None);

    opinion.Qualification.ShouldBe(Qualification.Of([DataSubjectRight.Access, DataSubjectRight.Erasure]));
  }

  /// <summary>
  /// Le lexique n'a aucun avis sur sa propre fiabilité et sa « raison » est une table de scores :
  /// l'adaptateur ne doit lui inventer ni confiance constante, ni justification.
  /// </summary>
  [Fact]
  public async Task LendsTheLexiconNeitherConfidenceNorJustification()
  {
    var sidecar = RespondingWith("""{"rights":["OutOfScope"],"engine":{"name":"lexicon","version":"1.0.0"}}""");

    var opinion = await Engine(sidecar).QualifyAsync(Text, CancellationToken.None);

    opinion.DeclaredConfidence.ShouldBeNull();
    opinion.Justification.ShouldBeNull();
  }

  [Theory]
  [InlineData(HttpStatusCode.BadRequest)]
  [InlineData(HttpStatusCode.InternalServerError)]
  [InlineData(HttpStatusCode.NotFound)]
  public async Task TreatsAnyNonSuccessAsAFailureOfTheEngine(HttpStatusCode status)
  {
    var sidecar = RespondingWith("""{"title":"Le moteur n'a pas rendu d'avis valide"}""", status);

    await Should.ThrowAsync<QualificationEngineFailure>(
      () => Engine(sidecar).QualifyAsync(Text, CancellationToken.None));
  }

  /// <summary>
  /// Les invariants sont re-portés de ce côté-ci de la frontière : le sidecar les tient déjà, mais
  /// un adaptateur qui leur ferait confiance laisserait passer un avis boiteux le jour où l'autre
  /// bout se tromperait.
  /// </summary>
  [Theory]
  [InlineData("""{"rights":[]}""")]
  [InlineData("""{"rights":["OutOfScope","Erasure"]}""")]
  [InlineData("""{"rights":["Deletion"]}""")]
  [InlineData("""{"rights":null}""")]
  [InlineData("""{"engine":{"name":"lexicon","version":"1.0.0"}}""")]
  [InlineData("ceci n'est pas du JSON")]
  public async Task TreatsAnythingThatIsNotAValidOpinionAsAFailureOfTheEngine(string body)
  {
    var sidecar = RespondingWith(body);

    await Should.ThrowAsync<QualificationEngineFailure>(
      () => Engine(sidecar).QualifyAsync(Text, CancellationToken.None));
  }

  /// <summary>
  /// L'annulation de l'appelant atteint le sidecar, et ressort telle quelle : ce n'est pas une
  /// panne du moteur, c'est un appelant qui est parti.
  /// </summary>
  [Fact]
  public async Task PropagatesTheCallersCancellationRatherThanCallingItAFailure()
  {
    using var cancellation = new CancellationTokenSource();
    await cancellation.CancelAsync();

    var sidecar = RespondingWith("""{"rights":["Erasure"]}""");

    await Should.ThrowAsync<OperationCanceledException>(
      () => Engine(sidecar).QualifyAsync(Text, cancellation.Token));
  }

  private static LexiconQualificationEngine Engine(SidecarDouble sidecar)
  {
    return new LexiconQualificationEngine(sidecar.Client());
  }

  private static SidecarDouble RespondingWith(string body, HttpStatusCode status = HttpStatusCode.OK)
  {
    return SidecarDouble.RespondingWith(body, status);
  }
}
