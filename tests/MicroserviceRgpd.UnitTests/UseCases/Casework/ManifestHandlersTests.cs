using Ardalis.Result;
using Ardalis.Specification;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.UseCases.Casework.DeclareSystem;
using MicroserviceRgpd.UseCases.Casework.ReviseSystem;
namespace MicroserviceRgpd.UnitTests.UseCases.Casework;

/// <summary>
/// Ce que les gestionnaires du <c>Manifest</c> décident, et ce qu'ils refusent de décider.
/// <para>
/// Ils sont exercés ici sur une horloge dictée : la date de déclaration est le seul élément du
/// catalogue que le service produit lui-même, et un test qui la lirait sur la machine qui l'exécute
/// ne prouverait rien de l'endroit d'où elle vient.
/// </para>
/// </summary>
public class ManifestHandlersTests
{
  private static readonly DateTimeOffset Today = new(2026, 8, 3, 14, 30, 0, TimeSpan.Zero);

  private readonly AClockStuckAt _clock = new(Today);
  private readonly IRepository<DeclaredSystem> _manifest = Substitute.For<IRepository<DeclaredSystem>>();

  /// <summary>
  /// La date vient de l'horloge du service, jamais de la saisie : la laisser déclarer permettrait
  /// de dater d'aujourd'hui une affirmation d'il y a deux ans.
  /// </summary>
  [Fact]
  public async Task DatesADeclarationOnTheServiceClockAndNeverOnWhatWasTyped()
  {
    var declared = await DeclareAsync([]);

    declared.IsSuccess.ShouldBeTrue();
    declared.Value.DeclaredOn.ShouldBe(Today);
  }

  [Fact]
  public async Task WritesTheDeclaredSystemToTheManifest()
  {
    await DeclareAsync([]);

    await _manifest.Received(1).AddAsync(
      Arg.Is<DeclaredSystem>(system => system.Id == DeclaredSystemId.From("export-agence")),
      Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// Le plancher <c>Locate</c> ressort <b>nommé</b>, sous le nom du champ fautif, et sans qu'aucune
  /// exception n'ait traversé la frontière : une case cochée de travers n'est pas une panne.
  /// </summary>
  [Fact]
  public async Task NamesTheLocateFloorAsAValidationErrorRatherThanThrowing()
  {
    var declared = await DeclareAsync([Capability.Erase]);

    declared.Status.ShouldBe(ResultStatus.Invalid);
    declared.ValidationErrors.ShouldContain(error => error.Identifier == "Capabilities");

    await _manifest.DidNotReceive().AddAsync(Arg.Any<DeclaredSystem>(), Arg.Any<CancellationToken>());
  }

  /// <summary>Un identifiant déjà pris est nommé à l'humain, et rien n'est écrit.</summary>
  [Fact]
  public async Task RefusesAnIdentifierTheManifestAlreadyCarries()
  {
    _manifest
      .FirstOrDefaultAsync(Arg.Any<ISingleResultSpecification<DeclaredSystem>>(), Arg.Any<CancellationToken>())
      .Returns(ASystem());

    var declared = await DeclareAsync([]);

    declared.Status.ShouldBe(ResultStatus.Invalid);
    declared.ValidationErrors.ShouldContain(error => error.Identifier == "Id");

    await _manifest.DidNotReceive().AddAsync(Arg.Any<DeclaredSystem>(), Arg.Any<CancellationToken>());
  }

  /// <summary>
  /// La révision redate sur la même horloge, et c'est bien la révision qui la déplace — le système
  /// avait été déclaré des mois plus tôt.
  /// </summary>
  [Fact]
  public async Task RedatesARevisedSystemOnTheServiceClock()
  {
    var declaredLongAgo = ASystem(Today.AddMonths(-8));

    _manifest
      .FirstOrDefaultAsync(Arg.Any<ISingleResultSpecification<DeclaredSystem>>(), Arg.Any<CancellationToken>())
      .Returns(declaredLongAgo);

    var revised = await new ReviseSystemHandler(_manifest, _clock).Handle(
      new ReviseSystemCommand(
        DeclaredSystemId.From("export-agence"),
        SystemLabel.From("L'export commercial mensuel"),
        SystemContents.From("Ce qu'il contient, revu aujourd'hui."),
        [],
        AdapterAddress: null),
      CancellationToken.None);

    revised.IsSuccess.ShouldBeTrue();
    declaredLongAgo.DeclaredOn.ShouldBe(Today);

    await _manifest.Received(1).UpdateAsync(declaredLongAgo, Arg.Any<CancellationToken>());
  }

  /// <summary>Réviser un système que personne n'a déclaré n'écrit rien et se dit comme tel.</summary>
  [Fact]
  public async Task RefusesToReviseASystemNobodyDeclared()
  {
    var revised = await new ReviseSystemHandler(_manifest, _clock).Handle(
      new ReviseSystemCommand(
        DeclaredSystemId.From("jamais-declare"),
        SystemLabel.From("Un fantôme"),
        SystemContents.From("Rien du tout."),
        [],
        AdapterAddress: null),
      CancellationToken.None);

    revised.Status.ShouldBe(ResultStatus.NotFound);

    await _manifest.DidNotReceive().UpdateAsync(Arg.Any<DeclaredSystem>(), Arg.Any<CancellationToken>());
  }

  private ValueTask<Result<DeclaredSystem>> DeclareAsync(IReadOnlyCollection<Capability> capabilities)
  {
    return new DeclareSystemHandler(_manifest, _clock).Handle(
      new DeclareSystemCommand(
        DeclaredSystemId.From("export-agence"),
        SystemLabel.From("L'export commercial mensuel"),
        SystemContents.From("L'export commercial transmis chaque mois à notre agence."),
        capabilities,
        AdapterAddress: null),
      CancellationToken.None);
  }

  private static DeclaredSystem ASystem(DateTimeOffset? declaredOn = null)
  {
    return DeclaredSystem.Declare(
      DeclaredSystemId.From("export-agence"),
      SystemLabel.From("L'export commercial mensuel"),
      SystemContents.From("L'export commercial transmis chaque mois à notre agence."),
      [],
      adapterAddress: null,
      declaredOn ?? Today);
  }
}
