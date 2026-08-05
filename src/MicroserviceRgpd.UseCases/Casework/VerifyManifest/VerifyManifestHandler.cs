using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;

namespace MicroserviceRgpd.UseCases.Casework.VerifyManifest;

/// <summary>
/// Confronte, système par système, ce que le <c>Manifest</c> déclare et ce que l'<c>Adapter</c> sert
/// — et porte la <b>sonde à secret délibérément faux</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle ne corrige jamais le <c>Manifest</c>, et la règle tient par la forme.</b> Ce type reçoit
/// un <see cref="IReadRepository{T}"/> : il n'a aucun moyen d'écrire, et personne n'a à jurer qu'il
/// ne l'a pas fait. Un écart est rapporté à un humain, qui tranche lequel des deux avait tort — le
/// catalogue qu'il a déclaré, ou le programme qui le sert.
/// </para>
/// <para>
/// <b>Rien n'entre au <c>Ledger</c>.</b> Ce qui est constaté ici vaut pour le déploiement entier :
/// l'attacher à un <c>Case</c> ferait dépendre la matière de preuve d'un geste d'exploitation, et
/// écrirait dans N dossiers ce qu'il n'y a qu'une fois à réparer.
/// </para>
/// <para>
/// <b>Le désaccord n'est pas non plus signalé par <see cref="IAdapterDisagreements"/>.</b> Ce
/// signal-là se tait après la première fois, exprès, parce qu'il double des appels que personne n'a
/// demandés ; une vérification, elle, est un geste délibéré dont on attend une réponse — et une
/// seconde passe qui n'aurait rien affiché aurait été lue comme « c'est réparé ».
/// </para>
/// <para>
/// <b>Les systèmes sans adresse d'<c>Adapter</c> sont passés</b>, sans appel et sans ligne : il n'y
/// a rien à confronter à un système traité à la main, et c'est le régime majoritaire.
/// </para>
/// <para>
/// <b>Un système à la fois.</b> Aucun parallélisme : le paysage compte quelques systèmes, et faire
/// frapper le service à toutes les portes du client en même temps serait la seule chose que cette
/// opération pourrait casser.
/// </para>
/// </remarks>
/// <param name="manifest">Le catalogue persisté, <b>en lecture seule</b>.</param>
/// <param name="probes">Les deux sondes non destructrices. Elles ne savent pas exercer autre chose que <c>Locate</c>.</param>
/// <param name="clock">L'horloge, injectée pour que la date du rapport se dicte en test.</param>
public sealed class VerifyManifestHandler(
  IReadRepository<DeclaredSystem> manifest,
  IAdapterProbes probes,
  TimeProvider clock)
  : IQueryHandler<VerifyManifestQuery, ManifestVerification>
{
  /// <inheritdoc />
  public async ValueTask<ManifestVerification> Handle(
    VerifyManifestQuery query,
    CancellationToken cancellationToken)
  {
    var verifications = new List<AdapterVerification>();

    foreach (var system in Manifest.Of(await manifest.ListAsync(cancellationToken)).Systems)
    {
      if (system.AdapterAddress is not { } address)
      {
        continue;
      }

      verifications.Add(await Confront(system, address, cancellationToken));
    }

    return new ManifestVerification(clock.GetUtcNow(), verifications);
  }

  /// <summary>
  /// Confronte un système à son <c>Adapter</c> : la sonde à secret faux d'abord, le plancher
  /// ensuite.
  /// </summary>
  /// <remarks>
  /// <b>L'ordre n'est pas indifférent.</b> On demande d'abord si la porte est gardée, et un
  /// <c>Adapter</c> trouvé nu n'est pas rappelé : il vient de servir un appel qu'il aurait dû
  /// refuser, ce qui répond déjà à la question du plancher, et un second aller-retour n'apprendrait
  /// rien qu'on ne sache — sinon que le service insiste auprès d'une application ouverte.
  /// </remarks>
  private async Task<AdapterVerification> Confront(
    DeclaredSystem system,
    AdapterAddress address,
    CancellationToken cancellationToken)
  {
    var exposure = await probes.ProbeWithAFalseSecretAsync(address, system.Id, cancellationToken);

    var locate = exposure == AdapterExposure.Naked
      ? Serves.Yes
      : await Floor(system, address, cancellationToken);

    return new AdapterVerification(system.Id, exposure, Compared(system, locate));
  }

  /// <summary>
  /// Le plancher, sous le secret du déploiement : l'<c>Adapter</c> sert-il <c>Locate</c> sur ce
  /// système ?
  /// </summary>
  /// <remarks>
  /// <b>Un refus de secret et une panne se rangent ensemble, et pas avec un refus de système.</b>
  /// Les deux premiers ne disent rien du catalogue — le déploiement est en désaccord avec lui-même,
  /// ou l'application est muette ; le troisième, lui, est une réponse claire : cet <c>Adapter</c> ne
  /// sert pas ce système, et c'est exactement l'écart qu'on est venu chercher.
  /// </remarks>
  private async Task<Serves> Floor(
    DeclaredSystem system,
    AdapterAddress address,
    CancellationToken cancellationToken)
  {
    try
    {
      var outcome = await probes.AskLocateAsync(address, system.Id, cancellationToken);

      if (outcome == AdapterOutcome.SystemNotServed)
      {
        return Serves.No;
      }

      return outcome == AdapterOutcome.SecretRefused ? Serves.Unknown : Serves.Yes;
    }
    catch (AdapterFailure)
    {
      // Une panne n'est ni un accord ni un écart. La taire rangerait ce système avec les conformes,
      // ce qui est exactement l'Omission silencieuse : une ligne manquante que nulle relecture ne
      // lève.
      return Serves.Unknown;
    }
  }

  /// <summary>
  /// Range les capacités en cause : celles que le catalogue déclare, et <c>Locate</c> lorsqu'il est
  /// servi sans être déclaré.
  /// </summary>
  /// <remarks>
  /// <b>Trois capacités sur quatre ne sont vérifiables par personne.</b> <c>Erase</c> et
  /// <c>Rectify</c> ne se constatent qu'en détruisant ou en réécrivant des données réelles ;
  /// <c>Read</c> ne se constate qu'en faisant entrer des données personnelles au titre d'une
  /// vérification. Elles sont donc rapportées <b>non vérifiables</b>, nommément — plutôt que passées
  /// sous silence, ce qui les aurait fait lire comme conformes.
  /// </remarks>
  private static IReadOnlyList<CapabilityVerification> Compared(DeclaredSystem system, Serves locate)
  {
    var declared = system.Capabilities;

    var compared = declared
      .Select(capability => new CapabilityVerification(
        capability,
        capability == Capability.Locate ? Agreement(locate) : CapabilityAgreement.Unverifiable))
      .ToList();

    if (!declared.Contains(Capability.Locate) && locate == Serves.Yes)
    {
      compared.Insert(0, new CapabilityVerification(Capability.Locate, CapabilityAgreement.NotDeclared));
    }

    return compared;
  }

  /// <summary>Ce qu'un plancher déclaré vaut, une fois confronté.</summary>
  private static CapabilityAgreement Agreement(Serves locate)
  {
    return locate switch
    {
      Serves.Yes => CapabilityAgreement.Agreed,
      Serves.No => CapabilityAgreement.NotServed,
      _ => CapabilityAgreement.Unknown,
    };
  }

  /// <summary>
  /// Ce que la sonde de plancher a établi. <b>Trois valeurs, jamais deux</b> : un booléen aurait
  /// rangé « on n'a pas pu savoir » avec « il ne sert pas », c'est-à-dire une ignorance avec un
  /// écart.
  /// </summary>
  private enum Serves
  {
    Unknown,
    Yes,
    No,
  }
}
