using System.Net;
using System.Text.Json;
using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.Qualifications;

/// <summary>
/// Le handler <c>POST /qualification?handler=Propose</c>, par lequel un autre écran fait qualifier
/// un texte et reçoit une <b>projection dédiée à l'écran</b> plutôt qu'une page.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce n'est pas le contrat public.</b> <c>QualifyResponse</c> ne bouge pas (ADR-0011) : la
/// projection ne porte ni identifiant de qualification, ni référence appelante, ni aucune phrase —
/// les phrases sont celles de l'écran qui l'appelle.
/// </para>
/// <para>
/// Le jeton anti-rejeu est celui <b>du tableau des demandes</b>, l'écran dont la modale appellera ce
/// handler : il vaut pour toute l'application.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class QualificationProposal
{
  private readonly CustomWebApplicationFactory<Program> _factory;
  private readonly QualificationSurface _surface;

  public QualificationProposal(CustomWebApplicationFactory<Program> factory)
  {
    _factory = factory;
    _surface = new QualificationSurface(factory);

    factory.Verdict.Reset();
    factory.Lexicon.Reset();
    factory.AuditTrail.Reset();
  }

  /// <summary>
  /// <b>Un droit se projette sous son nom canonique et son libellé français</b>, lus sur la
  /// taxonomie — et rien de la qualification ne permet de la retrouver : ni son identifiant, ni une
  /// référence appelante.
  /// </summary>
  [Fact]
  public async Task ProjectsASingleRightUnderItsCanonicalNameAndFrenchLabelAndNothingMore()
  {
    BothEnginesSee(DataSubjectRight.Erasure);
    _factory.Verdict.Justification = "Le texte réclame l'effacement de toutes les données.";

    var body = await ProposedAsync("Supprimez toutes mes données.");

    body.EnumerateObject().Select(field => field.Name).ShouldBe(
      ["rights", "reviewSignal", "degraded", "justification"],
      ignoreOrder: true);

    RightsOf(body).ShouldBe([(nameof(DataSubjectRight.Erasure), DataSubjectRight.Erasure.FrenchLabel)]);
    body.GetProperty("reviewSignal").GetString().ShouldBe(nameof(ReviewSignal.Corroborated));
    body.GetProperty("degraded").GetBoolean().ShouldBeFalse();
    body.GetProperty("justification").GetString().ShouldBe("Le texte réclame l'effacement de toutes les données.");
  }

  /// <summary>
  /// <b>Plusieurs droits se projettent dans l'ordre de la taxonomie</b>, et non dans celui où le
  /// moteur les a rendus.
  /// </summary>
  [Fact]
  public async Task ProjectsSeveralRightsInTheOrderOfTheTaxonomy()
  {
    var several = Qualification.Of([DataSubjectRight.Objection, DataSubjectRight.Access, DataSubjectRight.Erasure]);
    _factory.Verdict.Qualification = several;
    _factory.Lexicon.Qualification = several;

    var body = await ProposedAsync("Une copie, puis l'effacement, et je m'oppose à la prospection.");

    RightsOf(body).ShouldBe(
    [
      (nameof(DataSubjectRight.Access), DataSubjectRight.Access.FrenchLabel),
      (nameof(DataSubjectRight.Erasure), DataSubjectRight.Erasure.FrenchLabel),
      (nameof(DataSubjectRight.Objection), DataSubjectRight.Objection.FrenchLabel),
    ]);
  }

  /// <summary>
  /// <b><c>OutOfScope</c> s'exprime comme un élément seul</b>, jamais comme une liste vide : c'est un
  /// verdict, et l'écran qui le lit n'a pas à deviner ce qu'un vide voudrait dire.
  /// </summary>
  [Fact]
  public async Task ProjectsOutOfScopeAsASingleElement()
  {
    BothEnginesSee(DataSubjectRight.OutOfScope);

    var body = await ProposedAsync("Bonjour, quelles sont vos heures d'ouverture ?");

    RightsOf(body).ShouldBe([(nameof(DataSubjectRight.OutOfScope), DataSubjectRight.OutOfScope.FrenchLabel)]);
  }

  /// <summary>
  /// <b>Les deux axes se projettent séparément</b> : un service non entier le dit par
  /// <c>degraded</c>, et l'absence de justification se rend <c>null</c>.
  /// </summary>
  [Fact]
  public async Task ProjectsTheDegradedServiceAndTheAbsentJustification()
  {
    _factory.Verdict.Silence = new QualificationEngineFailure("Le moteur principal est resté muet.");
    _factory.Lexicon.Qualification = Qualification.Of([DataSubjectRight.Erasure]);

    var body = await ProposedAsync("Supprimez mes données.");

    body.GetProperty("reviewSignal").GetString().ShouldBe(nameof(ReviewSignal.NeedsReview));
    body.GetProperty("degraded").GetBoolean().ShouldBeTrue();
    body.GetProperty("justification").ValueKind.ShouldBe(JsonValueKind.Null);
  }

  /// <summary>
  /// <b>Deux moteurs qui divergent se projettent <c>Contested</c>.</b>
  /// </summary>
  [Fact]
  public async Task ProjectsAContestedReviewSignal()
  {
    _factory.Verdict.Qualification = Qualification.Of([DataSubjectRight.Erasure]);
    _factory.Lexicon.Qualification = Qualification.Of([DataSubjectRight.Access]);

    var body = await ProposedAsync("Effacez tout ce que vous avez sur moi.");

    body.GetProperty("reviewSignal").GetString().ShouldBe(nameof(ReviewSignal.Contested));
  }

  /// <summary>
  /// <b>Seul le texte envoyé atteint le moteur</b>, et la trace d'audit s'écrit comme pour tout
  /// verdict — <b>sans référence appelante</b> : l'écran n'est pas une application tierce.
  /// </summary>
  [Fact]
  public async Task HandsOnlyTheTextToTheEngineAndEngravesATraceWithoutCallerReference()
  {
    BothEnginesSee(DataSubjectRight.Access);
    var text = $"Je veux une copie de mes données. {Guid.NewGuid()}";

    await ProposedAsync(text);

    _factory.Verdict.ReceivedText.ShouldBe(RightsRequestText.From(text));
    _factory.Lexicon.ReceivedText.ShouldBe(RightsRequestText.From(text));

    using var scope = _factory.Services.CreateScope();

    var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().QualificationAuditEntries
      .AsNoTracking()
      .SingleAsync(entry => entry.Text == text);

    row.CallerReference.ShouldBeNull(
      "La référence appelante appartient à l'appelant : le service ne l'écrit jamais d'office.");
  }

  /// <summary>
  /// <b>Un texte vide ou blanc est refusé par un 400</b>, sans qu'aucun moteur soit appelé ni
  /// qu'aucune trace s'écrive — et sans phrase : c'est l'écran appelant qui nomme le refus.
  /// </summary>
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   \t  ")]
  public async Task RefusesAnEmptyTextWithoutCallingAnyEngineNorEngravingAnything(string? empty)
  {
    var before = await CountAuditRowsAsync();

    var response = await _surface.ProposeAsync(empty);

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    (await response.Content.ReadAsStringAsync()).ShouldNotContain("Le texte de la demande");

    _factory.Verdict.CallCount.ShouldBe(0);
    _factory.Lexicon.CallCount.ShouldBe(0);
    (await CountAuditRowsAsync()).ShouldBe(before);
  }

  /// <summary>
  /// <b>Un texte au-delà du plafond est refusé de même</b> — atteignable seulement par un envoi
  /// forgé, le Message d'une demande ayant le même plafond.
  /// </summary>
  [Fact]
  public async Task RefusesATextBeyondTheCeilingWithoutCallingAnyEngine()
  {
    var before = await CountAuditRowsAsync();

    var response = await _surface.ProposeAsync(new string('a', RightsRequestText.MaxLength + 1));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    _factory.Verdict.CallCount.ShouldBe(0);
    _factory.Lexicon.CallCount.ShouldBe(0);
    (await CountAuditRowsAsync()).ShouldBe(before);
  }

  /// <summary>
  /// ⚠️ <b>Sans jeton anti-rejeu, rien n'est qualifié</b> : une page tierce qui ferait poster le
  /// navigateur de l'<c>Operator</c> n'atteint pas le handler, comme pour les autres handlers.
  /// </summary>
  [Fact]
  public async Task QualifiesNothingWithoutTheAntiforgeryToken()
  {
    BothEnginesSee(DataSubjectRight.Access);

    var response = await _surface.ProposeWithoutTokenAsync("Je veux une copie de mes données.");

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    _factory.Verdict.CallCount.ShouldBe(0);
    _factory.Lexicon.CallCount.ShouldBe(0);
  }

  /// <summary>
  /// <b>Les deux moteurs muets rendent un 503</b>, et le message du moteur ne franchit pas la
  /// frontière : il nomme le moteur qui s'est tu, ce qui est de l'exploitation.
  /// </summary>
  [Fact]
  public async Task AnswersServiceUnavailableWithoutTheEngineMessageWhenBothEnginesAreMute()
  {
    _factory.Verdict.Silence = new QualificationEngineFailure("Le moteur principal est resté muet.");
    _factory.Lexicon.Silence = new QualificationEngineFailure("Le lexique est resté muet.");

    var response = await _surface.ProposeAsync("Supprimez mes données.");

    response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);

    var body = await response.Content.ReadAsStringAsync();

    body.ShouldNotContain("muet");
    body.ShouldNotContain(nameof(QualificationEngineFailure));
  }

  /// <summary>
  /// <b>Une échéance dépassée n'a pas de code à elle</b> : l'<c>Operator</c> relance dans les deux
  /// cas, et c'est encore un 503.
  /// </summary>
  [Fact]
  public async Task AnswersServiceUnavailableAlsoWhenTheDeadlineWasExceeded()
  {
    _factory.Verdict.Silence = new QualificationEngineDeadlineExceeded("Le moteur principal a dépassé son échéance.");
    _factory.Lexicon.Silence = new QualificationEngineFailure("Le lexique est resté muet.");

    var response = await _surface.ProposeAsync("Supprimez mes données.");

    response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
  }

  /// <summary>
  /// <b>Une trace qui ne s'écrit pas rend un 500</b>, jamais une proposition : un verdict dont
  /// personne ne pourrait répondre ne se propose pas. Le message de l'exception reste dans les traces.
  /// </summary>
  [Fact]
  public async Task AnswersInternalServerErrorWithoutTheExceptionMessageWhenTheTraceCannotBeWritten()
  {
    BothEnginesSee(DataSubjectRight.Erasure);
    _factory.AuditTrail.Refusal = new InvalidOperationException("La base est indisponible.");

    var response = await _surface.ProposeAsync("Supprimez mes données.");

    response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    (await response.Content.ReadAsStringAsync()).ShouldNotContain("La base est indisponible.");
  }

  /// <summary>
  /// <b>L'écran qui part interrompt le travail des moteurs</b>, et aucune trace ne s'écrit : la trace
  /// n'enregistre que des verdicts, et une qualification abandonnée n'en a rendu aucun.
  /// </summary>
  [Fact]
  public async Task InterruptsTheEnginesAndEngravesNothingWhenTheScreenLeaves()
  {
    _factory.Verdict.Delay = TimeSpan.FromSeconds(30);
    _factory.Lexicon.Delay = TimeSpan.FromSeconds(30);

    var before = await CountAuditRowsAsync();

    using var departure = new CancellationTokenSource();

    var proposing = _surface.ProposeAsync("Supprimez mes données.", departure.Token);

    await _factory.Verdict.Started.WaitAsync(TimeSpan.FromSeconds(10));
    await _factory.Lexicon.Started.WaitAsync(TimeSpan.FromSeconds(10));

    await departure.CancelAsync();

    await Should.ThrowAsync<OperationCanceledException>(() => proposing);

    await WaitUntilAsync(() => _factory.Verdict.Interrupted && _factory.Lexicon.Interrupted);
    (await CountAuditRowsAsync()).ShouldBe(before);
  }

  private void BothEnginesSee(DataSubjectRight right)
  {
    _factory.Verdict.Qualification = Qualification.Of([right]);
    _factory.Lexicon.Qualification = Qualification.Of([right]);
  }

  /// <summary>La proposition rendue pour <paramref name="text"/>, exigée en 200.</summary>
  private async Task<JsonElement> ProposedAsync(string text)
  {
    var response = await _surface.ProposeAsync(text);
    var body = await response.Content.ReadAsStringAsync();

    response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
    response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");

    return JsonDocument.Parse(body).RootElement;
  }

  private static (string Name, string Label)[] RightsOf(JsonElement body) =>
  [
    .. body.GetProperty("rights").EnumerateArray()
      .Select(right => (right.GetProperty("name").GetString()!, right.GetProperty("label").GetString()!)),
  ];

  private async Task<int> CountAuditRowsAsync()
  {
    using var scope = _factory.Services.CreateScope();

    return await scope.ServiceProvider.GetRequiredService<AppDbContext>()
      .QualificationAuditEntries.AsNoTracking().CountAsync();
  }

  private static async Task WaitUntilAsync(Func<bool> observed)
  {
    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

    while (!observed() && DateTime.UtcNow < deadline)
    {
      await Task.Delay(20);
    }

    observed().ShouldBeTrue();
  }
}
