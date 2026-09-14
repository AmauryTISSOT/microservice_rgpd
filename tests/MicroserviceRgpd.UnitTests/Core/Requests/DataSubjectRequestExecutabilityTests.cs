using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UnitTests.Core.Requests;

/// <summary>
/// <b>Une demande dit si elle s'exécute</b>, face à l'adresse — ou à l'absence d'adresse — que le
/// Paramétrage associe à son droit : « exécutable », ou le <b>premier</b> motif de blocage, dans
/// l'ordre demande close → identité non vérifiée → email manquant → aucune adresse (ADR-0026).
/// </summary>
/// <remarks>
/// ⚠️ <b>Les seize combinaisons des quatre conditions sont jouées</b> : un ordre qui ne se lit que
/// sur des cas isolés laisse passer une inversion entre deux motifs qui manquent ensemble.
/// </remarks>
public class DataSubjectRequestExecutabilityTests
{
  private static readonly DateTimeOffset Now = new(2026, 9, 11, 8, 15, 0, TimeSpan.Zero);

  private static readonly EndpointUrl Endpoint = EndpointUrl.From("https://brocanto.example.fr/rgpd/acces");

  /// <summary>Chaque combinaison des quatre conditions, et le motif attendu — <c>null</c> pour « exécutable ».</summary>
  public static TheoryData<bool, bool, bool, bool, string?> EveryCombination
  {
    get
    {
      var combinations = new TheoryData<bool, bool, bool, bool, string?>();

      foreach (var closed in new[] { false, true })
      {
        foreach (var verified in new[] { false, true })
        {
          foreach (var withEmail in new[] { false, true })
          {
            foreach (var withEndpoint in new[] { false, true })
            {
              var expected =
                closed ? nameof(ExecutionBlock.Closed)
                : !verified ? nameof(ExecutionBlock.IdentityNotVerified)
                : !withEmail ? nameof(ExecutionBlock.EmailMissing)
                : !withEndpoint ? nameof(ExecutionBlock.NoEndpoint)
                : null;

              combinations.Add(closed, verified, withEmail, withEndpoint, expected);
            }
          }
        }
      }

      return combinations;
    }
  }

  /// <summary>
  /// <b>En cours, identité vérifiée, avec un email, et un droit qui a une adresse</b> : la demande
  /// s'exécute, et aucun motif n'est rendu.
  /// </summary>
  [Fact]
  public void IsExecutableWhenEveryConditionHolds()
  {
    ARequest(closed: false, verified: true, withEmail: true)
      .ExecutionBlockFacing(Endpoint)
      .ShouldBeNull();
  }

  /// <summary>Le motif rendu est le premier qui manque, dans l'ordre de l'ADR-0026.</summary>
  [Theory]
  [MemberData(nameof(EveryCombination))]
  public void RendersTheFirstBlockInOrder(bool closed, bool verified, bool withEmail, bool withEndpoint, string? expected)
  {
    var block = ARequest(closed, verified, withEmail).ExecutionBlockFacing(withEndpoint ? Endpoint : null);

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

    request.ExecutionBlockFacing(Endpoint).ShouldBe(ExecutionBlock.Closed);
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
