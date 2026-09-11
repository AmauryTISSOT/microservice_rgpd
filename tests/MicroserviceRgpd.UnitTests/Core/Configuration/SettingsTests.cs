using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UnitTests.Core.Configuration;

/// <summary>
/// Le <see cref="Settings"/> — le Paramétrage — tient un seul invariant de domaine : <b>exactement
/// les six droits du périmètre, jamais <see cref="DataSubjectRight.OutOfScope"/></b>, et un service
/// vierge les rend tous « non configuré » sans qu'aucune ligne n'ait été persistée.
/// </summary>
public class SettingsTests
{
  /// <summary>
  /// <b>Les six droits, et eux seuls</b> — <see cref="DataSubjectRight.List"/> moins <see
  /// cref="DataSubjectRight.OutOfScope"/> —, dans l'ordre du noyau partagé.
  /// </summary>
  [Fact]
  public void ConfiguresExactlyTheSixInScopeRightsNeverOutOfScope()
  {
    Settings.ConfigurableRights.ShouldBe(
      [
        DataSubjectRight.Access,
        DataSubjectRight.Rectification,
        DataSubjectRight.Erasure,
        DataSubjectRight.Restriction,
        DataSubjectRight.Portability,
        DataSubjectRight.Objection,
      ]);

    Settings.ConfigurableRights.ShouldNotContain(DataSubjectRight.OutOfScope);
  }

  /// <summary>
  /// Le libellé et l'article se lisent <b>sur le droit</b>, seule source de vérité : le Paramétrage
  /// n'en tient aucune copie. On le vérifie sur les six articles arrêtés par le RGPD.
  /// </summary>
  [Fact]
  public void ReadsEachLabelAndArticleStraightFromTheSharedKernel()
  {
    var articles = Settings.ConfigurableRights.Select(right => right.Article);

    articles.ShouldBe([15, 16, 17, 18, 20, 21]);
    Settings.ConfigurableRights.ShouldAllBe(right => !string.IsNullOrWhiteSpace(right.FrenchLabel));
  }

  /// <summary>
  /// <b>Naissance paresseuse</b> : un service vierge rend les six droits « non configuré », sans
  /// qu'aucune adresse n'ait été écrite.
  /// </summary>
  [Fact]
  public void RendersTheSixRightsUnconfiguredOnAVirginService()
  {
    var settings = Settings.Unconfigured();

    settings.Rights.Select(entry => entry.Right).ShouldBe(Settings.ConfigurableRights);
    settings.Rights.ShouldAllBe(entry => !entry.IsConfigured);
    settings.Rights.ShouldAllBe(entry => entry.Endpoint == null);
  }

  /// <summary>La ligne unique porte toujours la clé figée du singleton.</summary>
  [Fact]
  public void CarriesTheFixedSingletonKey()
  {
    Settings.Unconfigured().Id.ShouldBe(Settings.SingletonId);
  }

  /// <summary>
  /// ⚠️ <b><see cref="DataSubjectRight.OutOfScope"/> n'a pas d'endpoint</b> : le lui demander est une
  /// programmation fautive, refusée plutôt que tolérée par une septième colonne muette.
  /// </summary>
  [Fact]
  public void RefusesToCarryAnEndpointForOutOfScope()
  {
    Should.Throw<ArgumentOutOfRangeException>(
      () => Settings.Unconfigured().EndpointFor(DataSubjectRight.OutOfScope));
  }
}
