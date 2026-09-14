using System.Net;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.TestDoubles.Ollama;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// Drapeau A2 allumé, <b>Ollama en panne au moment du dépôt collé</b> : un refus nommé « moteur de
/// détection indisponible », la saisie en place, et aucun rapport.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le lexique ne répond jamais à la place d'A2</b> (ADR-0025). Un repli à l'exécution ferait
/// changer de moteur un rapport dans le dos de l'<c>Operator</c> : la panne se dit, elle ne se
/// rattrape pas.
/// </para>
/// <para>
/// <b>L'hôte est partagé</b> par la collection A2 : chaque test remet Ollama en état en sortant, sans
/// quoi le suivant hériterait de sa panne.
/// </para>
/// <para>
/// L'échéance dépassée n'est pas jouée ici : l'hôte la règle à trente secondes, et la famille qu'elle
/// rend est la même que celle des autres causes — le moteur l'éprouve, sous une échéance courte.
/// </para>
/// </remarks>
[Collection(A2OnWebCollection.Name)]
public class A2UnavailableAtTheDepositScreen(A2OnWebApplicationFactory factory)
{
  private readonly ScreeningSurface _surface = new(factory);

  public static TheoryData<OllamaFault> TheFaults =>
  [
    OllamaFault.Unreachable,
    OllamaFault.AnotherEncoder,
    OllamaFault.MalformedEmbedding,
    OllamaFault.FailingEmbedding,
  ];

  /// <summary>
  /// Le refus nomme la famille, dit qu'aucun rapport n'a été produit, et <b>laisse le collage dans
  /// le champ</b> : l'<c>Operator</c> réessaie sans rien recoller.
  /// </summary>
  [Theory]
  [MemberData(nameof(TheFaults))]
  public async Task RefusesThePasteNamingTheUnavailableEngineAndKeepsItInPlace(OllamaFault fault)
  {
    var paste = APaste();

    var rendered = await DepositWhileOllamaFailsAsync(fault, paste);

    rendered.ShouldContain(ScreeningEngineUnavailable.FrenchLabel);
    rendered.ShouldContain("Aucun rapport de détection n'a été produit");
    rendered.ShouldContain("réessayez sans le recoller");

    // La saisie est rendue dans le champ, telle qu'elle a été collée.
    rendered.ShouldContain($">{paste}</textarea>");

    // Le relevé était sincère : le faire rejouer chez le client serait un trajet pour rien.
    rendered.ShouldNotContain("Aucune colonne n'a été ingérée");
  }

  /// <summary>
  /// ⚠️ <b>Aucun <c>Screening</c> n'est produit, et le rapport courant ne recule pas</b> : aucun
  /// arbitrage n'est perdu à cause d'une panne.
  /// </summary>
  [Fact]
  public async Task ProducesNoScreeningAndLeavesTheCurrentReportUnchanged()
  {
    factory.Ollama.Forget();
    await _surface.DepositAndReadTheReportAsync(APaste());
    var current = await _surface.CurrentScreeningAsync(table: "clients");
    var before = await CountedAsync();

    await DepositWhileOllamaFailsAsync(OllamaFault.Unreachable, APaste());

    (await CountedAsync()).ShouldBe(before, "Une panne du moteur ne crée ni rapport ni ligne.");
    (await _surface.CurrentScreeningAsync(table: "clients")).ShouldBe(
      current, "Le rapport courant ne recule pas : il est celui d'avant la panne.");
  }

  /// <summary>
  /// ⚠️ <b>Rien d'Ollama n'est cité à l'écran</b> — ni son message, ni son adresse, ni un digest :
  /// la panne se dit dans la langue du contexte.
  /// </summary>
  [Theory]
  [MemberData(nameof(TheFaults))]
  public async Task CitesNoTextFromOllamaNorItsAddressNorADigest(OllamaFault fault)
  {
    var rendered = await DepositWhileOllamaFailsAsync(fault, APaste());

    rendered.ShouldNotContain(OllamaDouble.Canary);
    rendered.ShouldNotContain("ollama", Case.Insensitive);
    rendered.ShouldNotContain(OllamaDouble.ForeignDigest[..12]);
    rendered.ShouldNotContain(A2Equivalence.EncoderDigest[..12]);
  }

  /// <summary>
  /// ⚠️ <b>Ollama injoignable ne fait jamais répondre le lexique</b> : le dépôt est refusé plutôt que
  /// redirigé vers un rapport, et aucun rapport du lexique n'apparaît.
  /// </summary>
  [Fact]
  public async Task NeverLetsTheLexiconAnswerWhenOllamaIsUnreachable()
  {
    var lexiconReportsBefore = await LexiconReportsAsync();
    factory.Ollama.Fault = OllamaFault.Unreachable;

    try
    {
      // « email » : le lexique l'aurait signalé à coup sûr.
      var deposited = await _surface.DepositAsync(ScreeningSurface.Paste(ScreeningSurface.Column("email", table: "clients")));

      deposited.StatusCode.ShouldBe(HttpStatusCode.OK, "Le dépôt est refusé à l'écran, et ne mène à aucun rapport.");
    }
    finally
    {
      factory.Ollama.Forget();
    }

    (await LexiconReportsAsync()).ShouldBe(lexiconReportsBefore, "Le lexique a détecté à la place d'A2.");
  }

  private static string APaste()
  {
    return ScreeningSurface.Paste(
      ScreeningSurface.Column("email", table: "clients", position: 1),
      ScreeningSurface.Column("price", table: "clients", position: 2));
  }

  private async Task<string> DepositWhileOllamaFailsAsync(OllamaFault fault, string paste)
  {
    factory.Ollama.Fault = fault;

    try
    {
      return await _surface.DepositAndReadTheRefusalAsync(paste);
    }
    finally
    {
      factory.Ollama.Forget();
    }
  }

  private async Task<(int Reports, int Columns)> CountedAsync()
  {
    using var scope = factory.Services.CreateScope();

    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    return (await database.Screenings.CountAsync(), await database.ScreenedColumns.CountAsync());
  }

  private async Task<int> LexiconReportsAsync()
  {
    using var scope = factory.Services.CreateScope();

    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    return (await database.Screenings.ToListAsync())
      .Count(screening => screening.Engine.Name == "regles-lexique-fr-en");
  }
}
