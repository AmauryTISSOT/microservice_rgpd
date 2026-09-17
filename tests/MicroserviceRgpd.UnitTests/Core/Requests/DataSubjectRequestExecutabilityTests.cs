using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UnitTests.Core.Requests;

/// <summary>
/// <b>Une demande dit si elle s'exécute</b>, face au canal d'exercice que le Paramétrage associe à
/// son droit : « exécutable », ou le <b>premier</b> motif de blocage, dans l'ordre demande close →
/// identité non vérifiée → email manquant → droit non configuré → exercice par RabbitMQ (ADR-0026,
/// ADR-0027).
/// </summary>
/// <remarks>
/// ⚠️ <b>Les vingt-quatre combinaisons des trois conditions et des trois canaux sont jouées</b> : un
/// ordre qui ne se lit que sur des cas isolés laisse passer une inversion entre deux motifs qui
/// manquent ensemble.
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

  /// <summary>
  /// Chaque combinaison des trois conditions et des trois canaux, et le motif attendu — <c>null</c>
  /// pour « exécutable ».
  /// </summary>
  public static TheoryData<bool, bool, bool, string, string?> EveryCombination
  {
    get
    {
      var combinations = new TheoryData<bool, bool, bool, string, string?>();

      foreach (var closed in new[] { false, true })
      {
        foreach (var verified in new[] { false, true })
        {
          foreach (var withEmail in new[] { false, true })
          {
            foreach (var channel in Channels.Keys)
            {
              var expected =
                closed ? nameof(ExecutionBlock.Closed)
                : !verified ? nameof(ExecutionBlock.IdentityNotVerified)
                : !withEmail ? nameof(ExecutionBlock.EmailMissing)
                : channel == "aucun" ? nameof(ExecutionBlock.RightNotConfigured)
                : channel == "rabbitmq" ? nameof(ExecutionBlock.RabbitMqNotYetSupported)
                : null;

              combinations.Add(closed, verified, withEmail, channel, expected);
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
      .ExecutionBlockFacing(Http)
      .ShouldBeNull();
  }

  /// <summary>
  /// ⚠️ <b>Un droit routé sur RabbitMQ est configuré, et pourtant bloqué</b> : le service ne sait pas
  /// encore publier, et le motif le dit — le dernier des cinq.
  /// </summary>
  [Fact]
  public void BlocksARightExercisedByRabbitMq()
  {
    ARequest(closed: false, verified: true, withEmail: true)
      .ExecutionBlockFacing(Routed)
      .ShouldBe(ExecutionBlock.RabbitMqNotYetSupported);
  }

  /// <summary>Le motif rendu est le premier qui manque, dans l'ordre de l'ADR-0026.</summary>
  [Theory]
  [MemberData(nameof(EveryCombination))]
  public void RendersTheFirstBlockInOrder(bool closed, bool verified, bool withEmail, string channel, string? expected)
  {
    var block = ARequest(closed, verified, withEmail).ExecutionBlockFacing(Channels[channel]);

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

    request.ExecutionBlockFacing(Http).ShouldBe(ExecutionBlock.Closed);
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
