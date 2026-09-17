using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UnitTests.Core.Configuration;

/// <summary>
/// Le <see cref="Settings"/> — le Paramétrage — tient deux invariants de domaine : <b>exactement les
/// six droits du périmètre, jamais <see cref="DataSubjectRight.OutOfScope"/></b>, et <b>un droit, un
/// seul canal</b> (ADR-0027). Un service vierge rend les six droits « non configuré » sans qu'aucune
/// ligne n'ait été persistée.
/// </summary>
public class SettingsTests
{
  private static readonly EndpointUrl Address = EndpointUrl.From("https://brocanto.example.fr/rgpd/acces");

  private static readonly ExerciseChannel Routing = new ExerciseChannel.RabbitMq(
    new RabbitMqRouting(ExchangeName.From("rgpd.exercice"), RoutingKey.From("droit.acces")));

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
  /// qu'aucun canal n'ait été écrit.
  /// </summary>
  [Fact]
  public void RendersTheSixRightsUnconfiguredOnAVirginService()
  {
    var settings = Settings.Unconfigured();

    settings.Rights.Select(entry => entry.Right).ShouldBe(Settings.ConfigurableRights);
    settings.Rights.ShouldAllBe(entry => !entry.IsConfigured);
    settings.Rights.ShouldAllBe(entry => entry.Channel is ExerciseChannel.NotConfigured);
  }

  /// <summary>La ligne unique porte toujours la clé figée du singleton.</summary>
  [Fact]
  public void CarriesTheFixedSingletonKey()
  {
    Settings.Unconfigured().Id.ShouldBe(Settings.SingletonId);
  }

  /// <summary>
  /// ⚠️ <b><see cref="DataSubjectRight.OutOfScope"/> n'a pas de canal</b> : le lui demander est une
  /// programmation fautive, refusée plutôt que tolérée par trois colonnes muettes.
  /// </summary>
  [Fact]
  public void RefusesToCarryAChannelForOutOfScope()
  {
    Should.Throw<ArgumentOutOfRangeException>(
      () => Settings.Unconfigured().ChannelFor(DataSubjectRight.OutOfScope));
  }

  /// <summary>
  /// <b>Écrire un droit ne touche que ce droit</b> : l'adresse posée se relit sur lui, et les cinq
  /// autres restent « non configuré ».
  /// </summary>
  [Fact]
  public void SetsTheChannelOfOneRightAndOfThatRightAlone()
  {
    var settings = Settings.Unconfigured();
    var endpoint = EndpointUrl.From("https://brocanto.example.fr/rgpd/effacement");

    settings.SetChannel(DataSubjectRight.Erasure, new ExerciseChannel.HttpEndpoint(endpoint));

    settings.ChannelFor(DataSubjectRight.Erasure).ShouldBe(new ExerciseChannel.HttpEndpoint(endpoint));
    settings.Rights
      .Where(entry => entry.Right != DataSubjectRight.Erasure)
      .ShouldAllBe(entry => !entry.IsConfigured);
  }

  /// <summary>
  /// Un <b>routage RabbitMQ</b> se pose et se relit comme tel, exchange et routing key compris, et ne
  /// touche aucun autre droit.
  /// </summary>
  [Fact]
  public void SetsARabbitMqRoutingOnOneRightAndOfThatRightAlone()
  {
    var settings = Settings.Unconfigured();

    settings.SetChannel(DataSubjectRight.Portability, Routing);

    settings.ChannelFor(DataSubjectRight.Portability).ShouldBe(Routing);
    settings.Rights
      .Where(entry => entry.Right != DataSubjectRight.Portability)
      .ShouldAllBe(entry => !entry.IsConfigured);
  }

  /// <summary>Un canal déjà posé se <b>remplace</b>, sans toucher aux autres droits configurés.</summary>
  [Fact]
  public void ReplacesAChannelAlreadySetWithoutTouchingTheOthers()
  {
    var settings = Settings.Unconfigured();

    settings.SetChannel(DataSubjectRight.Access, new ExerciseChannel.HttpEndpoint(Address));
    settings.SetChannel(
      DataSubjectRight.Objection,
      new ExerciseChannel.HttpEndpoint(EndpointUrl.From("https://brocanto.example.fr/v1")));
    settings.SetChannel(
      DataSubjectRight.Objection,
      new ExerciseChannel.HttpEndpoint(EndpointUrl.From("https://brocanto.example.fr/v2")));

    settings.ChannelFor(DataSubjectRight.Objection)
      .ShouldBe(new ExerciseChannel.HttpEndpoint(EndpointUrl.From("https://brocanto.example.fr/v2")));
    settings.ChannelFor(DataSubjectRight.Access).ShouldBe(new ExerciseChannel.HttpEndpoint(Address));
  }

  /// <summary>
  /// ⚠️ <b>Poser un routage efface l'adresse du même droit</b>, et <b>sans valeur dormante</b> : les
  /// propriétés plates de l'adresse sont rendues à <c>null</c>, de sorte qu'un effacement ultérieur du
  /// routage ne ressuscite rien (ADR-0027).
  /// </summary>
  [Fact]
  public void SettingARoutingErasesTheHttpAddressOfTheSameRightWithNoDormantValue()
  {
    var settings = Settings.Unconfigured();

    settings.SetChannel(DataSubjectRight.Access, new ExerciseChannel.HttpEndpoint(Address));
    settings.SetChannel(DataSubjectRight.Access, Routing);

    settings.ChannelFor(DataSubjectRight.Access).ShouldBe(Routing);
    settings.AccessUrl.ShouldBeNull();

    settings.ClearChannel(DataSubjectRight.Access);

    settings.ChannelFor(DataSubjectRight.Access).ShouldBe(ExerciseChannel.NotConfigured.Instance);
  }

  /// <summary>
  /// ⚠️ <b>Réciproquement, poser une adresse efface le routage du même droit</b>, exchange et routing
  /// key compris.
  /// </summary>
  [Fact]
  public void SettingAnAddressErasesTheRoutingOfTheSameRightWithNoDormantValue()
  {
    var settings = Settings.Unconfigured();

    settings.SetChannel(DataSubjectRight.Access, Routing);
    settings.SetChannel(DataSubjectRight.Access, new ExerciseChannel.HttpEndpoint(Address));

    settings.ChannelFor(DataSubjectRight.Access).ShouldBe(new ExerciseChannel.HttpEndpoint(Address));
    settings.AccessExchange.ShouldBeNull();
    settings.AccessRoutingKey.ShouldBeNull();
  }

  /// <summary>
  /// <b>Effacer un droit le ramène à « non configuré », et ne touche que lui</b> : l'autre droit
  /// configuré garde son canal. <b>Un seul geste pour les deux canaux</b> : un routage s'efface comme
  /// une adresse.
  /// </summary>
  [Fact]
  public void ClearsTheChannelOfOneRightAndOfThatRightAlone()
  {
    var settings = Settings.Unconfigured();

    settings.SetChannel(DataSubjectRight.Access, new ExerciseChannel.HttpEndpoint(Address));
    settings.SetChannel(DataSubjectRight.Erasure, Routing);

    settings.ClearChannel(DataSubjectRight.Erasure);

    settings.ChannelFor(DataSubjectRight.Erasure).ShouldBe(ExerciseChannel.NotConfigured.Instance);
    settings.ChannelFor(DataSubjectRight.Access).ShouldBe(new ExerciseChannel.HttpEndpoint(Address));
  }

  /// <summary>Effacer un droit déjà « non configuré » le laisse tel, sans erreur.</summary>
  [Fact]
  public void ClearingAnUnconfiguredRightLeavesItUnconfigured()
  {
    var settings = Settings.Unconfigured();

    settings.ClearChannel(DataSubjectRight.Restriction);

    settings.Rights.ShouldAllBe(entry => !entry.IsConfigured);
  }

  /// <summary>
  /// ⚠️ <b><see cref="DataSubjectRight.OutOfScope"/> n'a rien à effacer</b> : le lui demander est la
  /// même programmation fautive que de lui poser un canal.
  /// </summary>
  [Fact]
  public void RefusesToClearAChannelForOutOfScope()
  {
    Should.Throw<ArgumentOutOfRangeException>(
      () => Settings.Unconfigured().ClearChannel(DataSubjectRight.OutOfScope));
  }

  /// <summary>
  /// ⚠️ <b><see cref="DataSubjectRight.OutOfScope"/> ne reçoit pas de canal</b> : il n'y a pas de
  /// septième jeu de colonnes où l'écrire, et l'écrire nulle part serait une perte silencieuse. Les
  /// deux espèces sont refusées de la même façon.
  /// </summary>
  [Fact]
  public void RefusesToSetAChannelForOutOfScope()
  {
    Should.Throw<ArgumentOutOfRangeException>(
      () => Settings.Unconfigured().SetChannel(
        DataSubjectRight.OutOfScope,
        new ExerciseChannel.HttpEndpoint(Address)));

    Should.Throw<ArgumentOutOfRangeException>(
      () => Settings.Unconfigured().SetChannel(DataSubjectRight.OutOfScope, Routing));
  }

  /// <summary>
  /// La projection droit par droit est un <b>couple (droit, canal)</b>, et ne recopie ni le libellé
  /// ni l'article — ils se lisent sur le droit.
  /// </summary>
  [Fact]
  public void ProjectsEachRightWithItsChannel()
  {
    var settings = Settings.Unconfigured();

    settings.SetChannel(DataSubjectRight.Access, new ExerciseChannel.HttpEndpoint(Address));
    settings.SetChannel(DataSubjectRight.Erasure, Routing);

    settings.Rights.ShouldBe(
      [
        new RightChannel(DataSubjectRight.Access, new ExerciseChannel.HttpEndpoint(Address)),
        new RightChannel(DataSubjectRight.Rectification, ExerciseChannel.NotConfigured.Instance),
        new RightChannel(DataSubjectRight.Erasure, Routing),
        new RightChannel(DataSubjectRight.Restriction, ExerciseChannel.NotConfigured.Instance),
        new RightChannel(DataSubjectRight.Portability, ExerciseChannel.NotConfigured.Instance),
        new RightChannel(DataSubjectRight.Objection, ExerciseChannel.NotConfigured.Instance),
      ]);
  }
}
