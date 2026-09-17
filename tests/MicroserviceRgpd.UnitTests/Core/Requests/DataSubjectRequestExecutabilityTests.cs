using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UnitTests.Core.Requests;

/// <summary>
/// <b>Une demande dit si elle s'exécute</b>, face au canal d'exercice que le Paramétrage associe à
/// son droit <b>et à ce que le déploiement sait publier</b> : « exécutable », ou le <b>premier</b>
/// motif de blocage, dans l'ordre demande close → identité non vérifiée → email manquant → droit non
/// configuré → connexion au broker absente (ADR-0026, ADR-0027, ADR-0028).
/// </summary>
/// <remarks>
/// ⚠️ <b>Les quarante-huit combinaisons des trois conditions, des trois canaux et des deux
/// connexions sont jouées</b> : un ordre qui ne se lit que sur des cas isolés laisse passer une
/// inversion entre deux motifs qui manquent ensemble.
/// </remarks>
public class DataSubjectRequestExecutabilityTests
{
  private static readonly DateTimeOffset Now = new(2026, 9, 11, 8, 15, 0, TimeSpan.Zero);

  private static readonly ExerciseChannel Http = new ExerciseChannel.HttpEndpoint(
    EndpointUrl.From("https://brocanto.example.fr/rgpd/acces"));

  private static readonly ExerciseChannel Routed = new ExerciseChannel.RabbitMq(
    new RabbitMqRouting(ExchangeName.From("rgpd.exercice"), RoutingKey.From("droit.acces")));

  /// <summary>Les trois canaux, sous le nom que la théorie leur donne.</summary>
  private static readonly Dictionary<string, ExerciseChannel> Channels = new(StringComparer.Ordinal)
  {
    ["http"] = Http,
    ["rabbitmq"] = Routed,
    ["aucun"] = ExerciseChannel.NotConfigured.Instance,
  };

  /// <summary>Un déploiement qui déclare une connexion au broker : il sait publier.</summary>
  private static readonly BrokerConnection Connected = BrokerConnection.Configured.Instance;

  /// <summary>Un déploiement sans bus — un état légal, et non un manque à combler.</summary>
  private static readonly BrokerConnection Disconnected = BrokerConnection.Absent.Instance;

  /// <summary>
  /// Chaque combinaison des trois conditions, des trois canaux et des deux connexions, et le motif
  /// attendu — <c>null</c> pour « exécutable ».
  /// </summary>
  public static TheoryData<bool, bool, bool, string, bool, string?> EveryCombination
  {
    get
    {
      var combinations = new TheoryData<bool, bool, bool, string, bool, string?>();

      foreach (var closed in new[] { false, true })
      {
        foreach (var verified in new[] { false, true })
        {
          foreach (var withEmail in new[] { false, true })
          {
            foreach (var channel in Channels.Keys)
            {
              foreach (var connected in new[] { false, true })
              {
                var expected =
                  closed ? nameof(ExecutionBlock.Closed)
                  : !verified ? nameof(ExecutionBlock.IdentityNotVerified)
                  : !withEmail ? nameof(ExecutionBlock.EmailMissing)
                  : channel == "aucun" ? nameof(ExecutionBlock.RightNotConfigured)
                  : channel == "rabbitmq" && !connected ? nameof(ExecutionBlock.BrokerConnectionMissing)
                  : null;

                combinations.Add(closed, verified, withEmail, channel, connected, expected);
              }
            }
          }
        }
      }

      return combinations;
    }
  }

  /// <summary>
  /// <b>En cours, identité vérifiée, avec un email, et un droit qui a une adresse HTTP</b> : la
  /// demande s'exécute, et aucun motif n'est rendu.
  /// </summary>
  [Fact]
  public void IsExecutableWhenEveryConditionHolds()
  {
    ARequest(closed: false, verified: true, withEmail: true)
      .ExecutionBlockFacing(Http, Connected)
      .ShouldBeNull();
  }

  /// <summary>
  /// <b>Un droit routé s'exécute comme un droit adressé</b> dès lors que le déploiement déclare une
  /// connexion : aucun motif ne subsiste (ADR-0028).
  /// </summary>
  [Fact]
  public void IsExecutableOnARoutedRightWhenTheDeploymentCanPublish()
  {
    ARequest(closed: false, verified: true, withEmail: true)
      .ExecutionBlockFacing(Routed, Connected)
      .ShouldBeNull();
  }

  /// <summary>
  /// ⚠️ <b>Un droit routé sur un déploiement sans bus est bloqué</b> : le Paramétrage est bon, c'est
  /// la connexion qui manque — le cinquième et dernier motif.
  /// </summary>
  [Fact]
  public void BlocksARoutedRightWhenTheDeploymentHasNoBrokerConnection()
  {
    ARequest(closed: false, verified: true, withEmail: true)
      .ExecutionBlockFacing(Routed, Disconnected)
      .ShouldBe(ExecutionBlock.BrokerConnectionMissing);
  }

  /// <summary>
  /// ⚠️ <b>La connexion ne pèse que sur un droit routé</b> : un droit adressé en HTTP s'exécute sur un
  /// déploiement sans bus, et un droit non configuré reste non configuré sur un déploiement qui en a un.
  /// </summary>
  [Fact]
  public void LetsTheBrokerConnectionWeighOnlyOnARoutedRight()
  {
    var request = ARequest(closed: false, verified: true, withEmail: true);

    request.ExecutionBlockFacing(Http, Disconnected).ShouldBeNull();
    request.ExecutionBlockFacing(ExerciseChannel.NotConfigured.Instance, Connected)
      .ShouldBe(ExecutionBlock.RightNotConfigured);
  }

  /// <summary>⚠️ <b>La connexion est exigée</b> : sans elle, la question n'a pas de réponse.</summary>
  [Fact]
  public void RefusesToAnswerWithoutABrokerConnection()
  {
    Should.Throw<ArgumentNullException>(
      () => ARequest(closed: false, verified: true, withEmail: true).ExecutionBlockFacing(Http, null!));
  }

  /// <summary>Le motif rendu est le premier qui manque, dans l'ordre des ADR-0026, 0027 et 0028.</summary>
  [Theory]
  [MemberData(nameof(EveryCombination))]
  public void RendersTheFirstBlockInOrder(
    bool closed,
    bool verified,
    bool withEmail,
    string channel,
    bool connected,
    string? expected)
  {
    var block = ARequest(closed, verified, withEmail)
      .ExecutionBlockFacing(Channels[channel], connected ? Connected : Disconnected);

    block?.Name.ShouldBe(expected);
    (block is null).ShouldBe(expected is null);
  }

  /// <summary>
  /// <b>Une demande Annulée est close</b>, comme une Terminée : elle ne s'exécute pas davantage.
  /// </summary>
  [Theory]
  [InlineData(nameof(RequestStatus.Completed))]
  [InlineData(nameof(RequestStatus.Cancelled))]
  public void BlocksEitherClosedStatus(string status)
  {
    var request = ARequest(closed: false, verified: true, withEmail: true);
    SetStatus(request, RequestStatus.FromName(status));

    request.ExecutionBlockFacing(Http, Connected).ShouldBe(ExecutionBlock.Closed);
  }

  /// <summary>
  /// Une demande, son statut clos posé par réflexion quand il le faut : aucun geste ne sait encore
  /// clore une demande. Sans email, elle est identifiée par son nom et son prénom.
  /// </summary>
  private static DataSubjectRequest ARequest(bool closed, bool verified, bool withEmail)
  {
    var request = DataSubjectRequest.Receive(
      new DataSubjectRequestEntry(
        Origin: Origin.Email,
        ReceivedOn: "2026-09-10",
        LastName: "Dupont",
        FirstName: "Jeanne",
        Email: withEmail ? "jeanne.dupont@exemple.fr" : null,
        IdentityVerified: verified,
        Message: "Je souhaite accéder à mes données.",
        Right: nameof(DataSubjectRight.Access)),
      new DateOnly(2026, 9, 11),
      Now).Value;

    if (closed)
    {
      SetStatus(request, RequestStatus.Completed);
    }

    return request;
  }

  private static void SetStatus(DataSubjectRequest request, RequestStatus status) =>
    typeof(DataSubjectRequest).GetProperty(nameof(DataSubjectRequest.Status))!.SetValue(request, status);
}
