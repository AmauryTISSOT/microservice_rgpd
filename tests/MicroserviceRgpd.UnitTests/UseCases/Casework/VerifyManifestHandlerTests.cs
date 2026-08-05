using System.Reflection;
using NSubstitute.ExceptionExtensions;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.UseCases.Casework.VerifyManifest;

namespace MicroserviceRgpd.UnitTests.UseCases.Casework;

/// <summary>
/// Ce que la vérification du <c>Manifest</c> rapporte, et ce qu'elle refuse de faire.
/// </summary>
/// <remarks>
/// Les faits cardinaux : elle <b>ne corrige jamais</b> le catalogue, elle n'exerce <b>jamais</b>
/// autre chose qu'un <c>Locate</c>, et ce qu'elle constate est du <b>déploiement</b> — aucun
/// <c>Case</c>, aucun <c>Ledger</c>.
/// </remarks>
public class VerifyManifestHandlerTests
{
  private static readonly DateTimeOffset Now = new(2026, 8, 5, 9, 15, 0, TimeSpan.Zero);

  private static readonly AdapterAddress Brocanto = AdapterAddress.From("https://brocanto.example.fr/rgpd");

  private static readonly DeclaredSystemId Boutique = DeclaredSystemId.From("boutique");

  private readonly IReadRepository<DeclaredSystem> _manifest = Substitute.For<IReadRepository<DeclaredSystem>>();
  private readonly IAdapterProbes _probes = Substitute.For<IAdapterProbes>();

  /// <summary>
  /// Le cas conforme : la porte est gardée, et le plancher déclaré est servi. Rien à trancher.
  /// </summary>
  [Fact]
  public async Task ReportsAgreementWhenTheAdapterGuardsItsDoorAndServesWhatIsDeclared()
  {
    Declared(ASystem([Capability.Locate]));
    TheDoorIs(AdapterExposure.Guarded);
    TheFloorAnswers(AdapterOutcome.Served);

    var verified = await Verifying();

    var boutique = verified.Adapters.ShouldHaveSingleItem();

    boutique.DeclaredSystem.ShouldBe(Boutique);
    boutique.Exposure.ShouldBe(AdapterExposure.Guarded);
    boutique.Disagrees.ShouldBeFalse();
    Of(boutique, Capability.Locate).ShouldBe(CapabilityAgreement.Agreed);
  }

  /// <summary>
  /// L'écart : le catalogue déclare un plancher que l'<c>Adapter</c> ne sert pas. Il est
  /// <b>rapporté</b>, et le <c>Manifest</c> n'est pas touché — un humain tranche lequel des deux
  /// avait tort.
  /// </summary>
  [Fact]
  public async Task ReportsTheGapWhenTheAdapterDoesNotServeTheDeclaredSystem()
  {
    Declared(ASystem([Capability.Locate, Capability.Erase]));
    TheDoorIs(AdapterExposure.Guarded);
    TheFloorAnswers(AdapterOutcome.SystemNotServed);

    var verified = await Verifying();

    var boutique = verified.Adapters.ShouldHaveSingleItem();

    boutique.Disagrees.ShouldBeTrue();
    verified.Disagreeing.ShouldHaveSingleItem();
    Of(boutique, Capability.Locate).ShouldBe(CapabilityAgreement.NotServed);
  }

  /// <summary>
  /// L'écart inverse, le plus proche de l'<c>Omission silencieuse</c> : l'<c>Adapter</c> sert un
  /// plancher que personne n'a déclaré, et le service ne s'en servira jamais faute de l'avoir lu.
  /// </summary>
  [Fact]
  public async Task ReportsTheReverseGapWhenTheAdapterServesWhatTheCatalogueDoesNotDeclare()
  {
    Declared(ASystem([]));
    TheDoorIs(AdapterExposure.Guarded);
    TheFloorAnswers(AdapterOutcome.Served);

    var boutique = (await Verifying()).Adapters.ShouldHaveSingleItem();

    boutique.Disagrees.ShouldBeTrue();
    Of(boutique, Capability.Locate).ShouldBe(CapabilityAgreement.NotDeclared);
  }

  /// <summary>
  /// La sonde à secret délibérément faux : un <c>Adapter</c> qui a servi sous un secret invalide est
  /// <b>nu</b>, et cela se lit avant tout le reste.
  /// </summary>
  [Fact]
  public async Task ReportsANakedAdapterWhenTheFalseSecretIsServedAnyway()
  {
    Declared(ASystem([Capability.Locate]));
    TheDoorIs(AdapterExposure.Naked);

    var verified = await Verifying();

    verified.Naked.ShouldHaveSingleItem().DeclaredSystem.ShouldBe(Boutique);
  }

  /// <summary>
  /// Un <c>Adapter</c> trouvé nu n'est <b>pas rappelé</b> : il vient de servir un appel qu'il aurait
  /// dû refuser, ce qui répond déjà à la question du plancher. Insister n'apprendrait rien, sinon
  /// que le service frappe deux fois à une porte ouverte.
  /// </summary>
  [Fact]
  public async Task DoesNotCallANakedAdapterASecondTime()
  {
    Declared(ASystem([Capability.Locate]));
    TheDoorIs(AdapterExposure.Naked);

    await Verifying();

    await _probes.DidNotReceiveWithAnyArgs()
      .AskLocateAsync(Brocanto, Boutique, CancellationToken.None);
  }

  /// <summary>
  /// <c>Erase</c>, <c>Rectify</c> et <c>Read</c> sont rapportées <b>non vérifiables</b>, nommément.
  /// Les taire les aurait fait lire comme conformes, ce qui est la forme même de l'<c>Omission
  /// silencieuse</c> : une ligne manquante que nulle relecture ne lève.
  /// </summary>
  [Theory]
  [InlineData(nameof(Capability.Read))]
  [InlineData(nameof(Capability.Erase))]
  [InlineData(nameof(Capability.Rectify))]
  public async Task NamesTheCapabilitiesThatNoVerificationWillEverReach(string capability)
  {
    var unreachable = Capability.FromName(capability);

    Declared(ASystem([Capability.Locate, unreachable]));
    TheDoorIs(AdapterExposure.Guarded);
    TheFloorAnswers(AdapterOutcome.Served);

    var boutique = (await Verifying()).Adapters.ShouldHaveSingleItem();

    Of(boutique, unreachable).ShouldBe(CapabilityAgreement.Unverifiable);
    boutique.Disagrees.ShouldBeFalse();
  }

  /// <summary>
  /// Un secret refusé par le déploiement ne dit <b>rien</b> du catalogue : c'est un fait de
  /// topologie. Le compter comme un écart ferait tenir pour menteur un catalogue dont on n'a rien
  /// su vérifier.
  /// </summary>
  [Fact]
  public async Task ConcludesNothingOnTheCatalogueWhenTheDeploymentSecretIsRefused()
  {
    Declared(ASystem([Capability.Locate]));
    TheDoorIs(AdapterExposure.Guarded);
    TheFloorAnswers(AdapterOutcome.SecretRefused);

    var boutique = (await Verifying()).Adapters.ShouldHaveSingleItem();

    Of(boutique, Capability.Locate).ShouldBe(CapabilityAgreement.Unknown);
    boutique.Disagrees.ShouldBeFalse();
  }

  /// <summary>
  /// Une panne n'est ni un accord ni un écart, et elle reste une <b>ligne présente</b> : la taire
  /// rangerait ce système avec les conformes.
  /// </summary>
  [Fact]
  public async Task KeepsTheLineWhenTheAdapterAnswersNothingAtAll()
  {
    Declared(ASystem([Capability.Locate]));
    TheDoorIs(AdapterExposure.Inconclusive);

    _probes.AskLocateAsync(Brocanto, Boutique, CancellationToken.None)
      .ThrowsAsyncForAnyArgs(new AdapterFailure("muet"));

    var boutique = (await Verifying()).Adapters.ShouldHaveSingleItem();

    boutique.Exposure.ShouldBe(AdapterExposure.Inconclusive);
    Of(boutique, Capability.Locate).ShouldBe(CapabilityAgreement.Unknown);
  }

  /// <summary>
  /// <b>Les systèmes sans adresse d'<c>Adapter</c> ne sont pas appelés</b>, et ne font aucune ligne :
  /// il n'y a rien à confronter à un système traité à la main, et c'est le régime majoritaire.
  /// </summary>
  [Fact]
  public async Task PassesOverTheSystemsThatNoAdapterServes()
  {
    Declared(DeclaredSystem.Declare(
      DeclaredSystemId.From("export-agence"),
      SystemLabel.From("L'export commercial"),
      SystemContents.From("Le fichier transmis chaque mois à notre agence."),
      [],
      adapterAddress: null,
      Now.AddYears(-1)));

    var verified = await Verifying();

    verified.Adapters.ShouldBeEmpty();
    await _probes.DidNotReceiveWithAnyArgs()
      .ProbeWithAFalseSecretAsync(Brocanto, Boutique, CancellationToken.None);
  }

  /// <summary>Le rapport est daté de l'horloge du service : il se relit à côté des dates de déclaration.</summary>
  [Fact]
  public async Task DatesTheReportOnTheServiceClock()
  {
    Declared();

    (await Verifying()).VerifiedOn.ShouldBe(Now);
  }

  /// <summary>
  /// <b>Elle ne corrige jamais le <c>Manifest</c></b>, et la règle tient par la <b>forme</b> : ce
  /// gestionnaire ne reçoit aucun dépôt en écriture, et n'a donc rien à réécrire. Personne n'a à
  /// jurer qu'il ne l'a pas fait.
  /// </summary>
  [Fact]
  public void HasNoWayOfCorrectingTheManifestItVerifies()
  {
    var collaborators = typeof(VerifyManifestHandler)
      .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
      .Single()
      .GetParameters()
      .Select(parameter => parameter.ParameterType)
      .ToArray();

    collaborators.ShouldNotContain(typeof(IRepository<DeclaredSystem>));
    collaborators.ShouldContain(typeof(IReadRepository<DeclaredSystem>));
  }

  /// <summary>
  /// <b>Aucune sonde ne peut exercer <c>Erase</c> ni <c>Rectify</c></b>, et la règle tient par la
  /// forme : le port ne reçoit pas de <c>Capability</c>, et il n'existe aucun emplacement où en
  /// glisser une. Un garde écrit dans un corps de méthode aurait fait dépendre la promesse d'une
  /// relecture, sur la seule surface où une erreur détruit des données réelles.
  /// </summary>
  [Fact]
  public void HasNoWayOfExercisingAnythingButTheFloor()
  {
    typeof(IAdapterProbes)
      .GetMethods()
      .SelectMany(method => method.GetParameters())
      .Select(parameter => parameter.ParameterType)
      .ShouldNotContain(typeof(Capability));
  }

  private async Task<ManifestVerification> Verifying()
  {
    return await new VerifyManifestHandler(_manifest, _probes, new AClockStuckAt(Now))
      .Handle(new VerifyManifestQuery(), CancellationToken.None);
  }

  private void Declared(params DeclaredSystem[] systems)
  {
    _manifest.ListAsync(CancellationToken.None).ReturnsForAnyArgs(new List<DeclaredSystem>(systems));
  }

  private void TheDoorIs(AdapterExposure exposure)
  {
    _probes.ProbeWithAFalseSecretAsync(Brocanto, Boutique, CancellationToken.None)
      .ReturnsForAnyArgs(exposure);
  }

  private void TheFloorAnswers(AdapterOutcome outcome)
  {
    _probes.AskLocateAsync(Brocanto, Boutique, CancellationToken.None)
      .ReturnsForAnyArgs(outcome);
  }

  private static DeclaredSystem ASystem(IEnumerable<Capability> capabilities)
  {
    return DeclaredSystem.Declare(
      Boutique,
      SystemLabel.From("La boutique"),
      SystemContents.From("Les comptes clients et les commandes."),
      capabilities,
      Brocanto,
      Now.AddMonths(-6));
  }

  /// <summary>Ce que la vérification a constaté de cette capacité, ou l'échec du test si elle n'en dit rien.</summary>
  private static CapabilityAgreement Of(AdapterVerification verified, Capability capability)
  {
    return verified.Capabilities
      .Where(entry => entry.Capability == capability)
      .Select(entry => entry.Agreement)
      .ToArray()
      .ShouldHaveSingleItem();
  }
}
