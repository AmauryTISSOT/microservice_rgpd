using System.Text.Json;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Infrastructure.Casework.Adapters;
using MicroserviceRgpd.UseCases.Casework.VerifyManifest;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Casework;

/// <summary>
/// La vérification du <c>Manifest</c> de bout en bout, sur la <b>couture du fil</b> : le
/// gestionnaire réel, les sondes réelles, et un <c>Adapter</c> de client à l'autre bout.
/// </summary>
/// <remarks>
/// <para>
/// <b>La doublure est posée sur le fil, et pas au-dessus.</b> Ce qui est vérifié ici est ce que
/// l'intégrateur a implémenté — l'en-tête de secret comparé, le <c>system_id</c> en paramètre, le
/// <c>401</c> et le <c>404</c> qui ne se confondent pas. Doubler le port du domaine aurait fait
/// prouver à ces tests le comportement d'une doublure, alors que <b>l'appel au faux secret
/// n'existe que pour ce qui se passe sur le fil</b>.
/// </para>
/// <para>
/// Les trois cas du contrat s'y lisent : un <c>Adapter</c> conforme, un <c>Adapter</c> en écart, et
/// un <c>Adapter</c> nu.
/// </para>
/// </remarks>
public class ManifestVerificationOnTheWireTests
{
  private const string Secret = "un-secret-partage";

  private static readonly DateTimeOffset Now = new(2026, 8, 5, 9, 15, 0, TimeSpan.Zero);

  private static readonly AdapterAddress Brocanto = AdapterAddress.From("https://brocanto.example.fr/rgpd");

  /// <summary>
  /// <b>L'<c>Adapter</c> conforme</b> : il refuse le secret faux, puis sert le plancher déclaré sous
  /// le vrai. Le catalogue et le programme qui le sert disent la même chose.
  /// </summary>
  [Fact]
  public async Task ReadsAConformingAdapterAsGuardedAndInAgreement()
  {
    var adapter = AnAdapterOnTheWire.Guarding(Secret, "boutique");

    var boutique = (await Verifying(adapter, ASystem([Capability.Locate]))).Adapters.ShouldHaveSingleItem();

    boutique.Exposure.ShouldBe(AdapterExposure.Guarded);
    boutique.Disagrees.ShouldBeFalse();
    boutique.Capabilities.ShouldHaveSingleItem().Agreement.ShouldBe(CapabilityAgreement.Agreed);
  }

  /// <summary>
  /// <b>L'<c>Adapter</c> en écart</b> : il garde bien sa porte, mais ne sert pas le système que le
  /// catalogue lui attribue. L'écart est <b>rapporté</b> — le <c>Manifest</c> n'est pas retouché, et
  /// un humain tranchera lequel des deux avait tort.
  /// </summary>
  [Fact]
  public async Task ReadsAnAdapterThatDoesNotServeTheDeclaredSystemAsADisagreement()
  {
    var adapter = AnAdapterOnTheWire.Guarding(Secret, "brocanto-journal");

    var verified = await Verifying(adapter, ASystem([Capability.Locate]));
    var boutique = verified.Adapters.ShouldHaveSingleItem();

    boutique.Exposure.ShouldBe(AdapterExposure.Guarded);
    boutique.Disagrees.ShouldBeTrue();
    verified.Disagreeing.ShouldHaveSingleItem();
    boutique.Capabilities.ShouldHaveSingleItem().Agreement.ShouldBe(CapabilityAgreement.NotServed);
  }

  /// <summary>
  /// <b>L'<c>Adapter</c> nu</b> : il a servi un appel présenté avec un secret que le contrat lui
  /// demandait de refuser. C'est un résultat d'exploitation, et il se lit avant tout le reste.
  /// </summary>
  [Fact]
  public async Task ReadsAnAdapterThatServesAFalseSecretAsNaked()
  {
    var adapter = AnAdapterOnTheWire.Naked();

    var verified = await Verifying(adapter, ASystem([Capability.Locate]));
    var boutique = verified.Naked.ShouldHaveSingleItem();

    boutique.DeclaredSystem.ShouldBe(DeclaredSystemId.From("boutique"));

    // Une seule requête : un Adapter trouvé nu n'est pas rappelé sous le vrai secret.
    adapter.Asked.ShouldHaveSingleItem();

    // ⚠️ Et l'on ne conclut rien de son catalogue : cette doublure sert 200 à tout le monde, système
    // inconnu compris. Lire son 200 comme « déclarée et servie » écrirait un accord que personne
    // n'a constaté.
    boutique.Capabilities.ShouldHaveSingleItem().Agreement.ShouldBe(CapabilityAgreement.Unknown);
  }

  /// <summary>
  /// Le secret de la sonde n'a <b>aucun rapport</b> avec celui du déploiement : un faux dérivé du
  /// vrai le livrerait, octet par octet, à l'<c>Adapter</c> même dont on soupçonne qu'il ne garde
  /// rien.
  /// </summary>
  [Fact]
  public async Task PresentsAFalseSecretThatTellsNothingOfTheRealOne()
  {
    var adapter = AnAdapterOnTheWire.Guarding(Secret, "boutique");

    await Verifying(adapter, ASystem([Capability.Locate]));

    var probed = adapter.Asked[0].Headers.GetValues(AdapterWire.SecretHeader).ShouldHaveSingleItem();

    probed.ShouldBe(HttpAdapterProbes.FalseSecret);
    probed.ShouldNotContain(Secret);
    Secret.ShouldNotContain(probed);

    // Puis, l'Adapter ayant gardé sa porte, le plancher est demandé sous le vrai secret.
    adapter.Asked[1].Headers.GetValues(AdapterWire.SecretHeader).ShouldBe([Secret]);
  }

  /// <summary>
  /// <b>Aucune sonde n'exerce autre chose qu'un <c>Locate</c>, et aucune ne cherche personne.</b>
  /// Sur le fil, cela se lit à deux endroits : le chemin appelé, et un sac de désignations vide —
  /// aucune personne réelle ne part chez un client au titre d'aucun dossier.
  /// </summary>
  [Fact]
  public async Task NeverExercisesAnythingButTheFloorAndNeverUnderAnyone()
  {
    var adapter = AnAdapterOnTheWire.Guarding(Secret, "boutique");

    await Verifying(adapter, ASystem([Capability.Locate, Capability.Read, Capability.Erase, Capability.Rectify]));

    foreach (var asked in adapter.Asked)
    {
      asked.RequestUri!.AbsolutePath.ShouldBe("/rgpd/locate");
      asked.RequestUri.Query.ShouldBe("?system_id=boutique");
    }

    foreach (var body in adapter.Bodies)
    {
      using var sent = JsonDocument.Parse(body!);

      sent.RootElement.GetProperty("designations").GetArrayLength().ShouldBe(0);
      sent.RootElement.EnumerateObject().Select(field => field.Name).ShouldBe(["designations"]);
    }
  }

  private static async Task<ManifestVerification> Verifying(AnAdapterOnTheWire adapter, DeclaredSystem system)
  {
    var manifest = Substitute.For<IReadRepository<DeclaredSystem>>();

    manifest.ListAsync(CancellationToken.None).ReturnsForAnyArgs([system]);

    var probes = new HttpAdapterProbes(adapter.Client(), new AdapterSecret(Secret));

    return await new VerifyManifestHandler(manifest, probes, new AClockStuckAt(Now))
      .Handle(new VerifyManifestQuery(), CancellationToken.None);
  }

  private static DeclaredSystem ASystem(IEnumerable<Capability> capabilities)
  {
    return DeclaredSystem.Declare(
      DeclaredSystemId.From("boutique"),
      SystemLabel.From("La boutique"),
      SystemContents.From("Les comptes clients et les commandes."),
      capabilities,
      Brocanto,
      Now.AddMonths(-6));
  }
}
