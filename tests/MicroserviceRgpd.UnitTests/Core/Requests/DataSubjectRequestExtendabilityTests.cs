using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UnitTests.Core.Requests;

/// <summary>
/// <b>Une demande dit si elle se prolonge</b>, contre le jour qu'il est à Paris : « prolongeable »,
/// ou le <b>premier</b> motif de blocage, dans l'ordre demande close → demande déjà prolongée → date
/// limite de réponse dépassée (ADR-0029).
/// </summary>
/// <remarks>
/// ⚠️ <b>Les douze combinaisons des trois conditions sont jouées</b> : un ordre qui ne se lit que sur
/// des cas isolés laisse passer une inversion entre deux motifs qui manquent ensemble.
/// </remarks>
public class DataSubjectRequestExtendabilityTests
{
  private static readonly DateTimeOffset Now = new(2026, 9, 11, 8, 15, 0, TimeSpan.Zero);

  /// <summary>La date limite de réponse d'une demande reçue le 10 septembre 2026.</summary>
  private static readonly DateOnly Deadline = new(2026, 10, 10);

  /// <summary>
  /// Chaque combinaison des trois conditions, et le motif attendu — <c>null</c> pour
  /// « prolongeable ».
  /// </summary>
  public static TheoryData<bool, bool, string, string?> EveryCombination
  {
    get
    {
      var combinations = new TheoryData<bool, bool, string, string?>();

      foreach (var closed in new[] { false, true })
      {
        foreach (var extended in new[] { false, true })
        {
          foreach (var day in new[] { "2026-10-09", "2026-10-10", "2026-10-11" })
          {
            var elapsed = DateOnly.Parse(day, System.Globalization.CultureInfo.InvariantCulture) > Deadline;

            combinations.Add(
              closed,
              extended,
              day,
              closed ? nameof(ExtensionBlock.Closed)
              : extended ? nameof(ExtensionBlock.AlreadyExtended)
              : elapsed ? nameof(ExtensionBlock.DeadlineElapsed)
              : null);
          }
        }
      }

      return combinations;
    }
  }

  /// <summary>
  /// ⚠️ <b>Seule la première condition qui manque se dit</b> : une demande close et déjà prolongée
  /// dit « Demande close », et rien d'autre.
  /// </summary>
  [Theory]
  [MemberData(nameof(EveryCombination))]
  public void SaysTheFirstBlockAndOnlyIt(bool closed, bool extended, string today, string? block)
  {
    var request = ARequest(closed, extended);

    request.ExtensionBlockFacing(DateOnly.Parse(today, System.Globalization.CultureInfo.InvariantCulture))
      .ShouldBe(block is null ? null : ExtensionBlock.FromName(block));
  }

  /// <summary>
  /// <b>Le jour limite est accepté, le lendemain est refusé</b> : l'<c>Operator</c> ne perd pas le
  /// dernier jour que le règlement lui accorde.
  /// </summary>
  [Fact]
  public void AcceptsTheDeadlineDayAndRefusesTheDayAfter()
  {
    var request = ARequest(closed: false, extended: false);

    request.ExtensionBlockFacing(Deadline).ShouldBeNull();
    request.ExtensionBlockFacing(Deadline.AddDays(1)).ShouldBe(ExtensionBlock.DeadlineElapsed);
  }

  /// <summary>Les deux statuts clos bloquent, et non le seul « Terminée ».</summary>
  [Theory]
  [InlineData(nameof(RequestStatus.Completed))]
  [InlineData(nameof(RequestStatus.Cancelled))]
  public void BlocksEitherClosedStatus(string status)
  {
    var request = ARequest(closed: false, extended: false);
    SetStatus(request, RequestStatus.FromName(status));

    request.ExtensionBlockFacing(Deadline).ShouldBe(ExtensionBlock.Closed);
  }

  /// <summary>
  /// ⚠️ <b>Une demande prolongée l'est pour de bon</b> : sa date limite reportée rouvrirait la
  /// fenêtre, et c'est justement ce que « déjà prolongée » interdit.
  /// </summary>
  [Fact]
  public void RefusesASecondExtensionEvenInsideTheReportedWindow()
  {
    var request = ARequest(closed: false, extended: true);

    request.ResponseDeadline.ShouldBe(new DateOnly(2026, 12, 10));
    request.ExtensionBlockFacing(new DateOnly(2026, 11, 1)).ShouldBe(ExtensionBlock.AlreadyExtended);
  }

  /// <summary>
  /// Une demande reçue le 10 septembre 2026, son statut clos posé par réflexion quand il le faut :
  /// aucun geste ne sait encore clore une demande sans l'exécuter.
  /// </summary>
  private static DataSubjectRequest ARequest(bool closed, bool extended)
  {
    var request = DataSubjectRequest.Receive(
      new DataSubjectRequestEntry(
        Origin: Origin.Email,
        ReceivedOn: "2026-09-10",
        LastName: "Dupont",
        FirstName: "Jeanne",
        Email: "jeanne.dupont@exemple.fr",
        IdentityVerified: true,
        Message: "Je souhaite accéder à mes données.",
        Right: nameof(DataSubjectRight.Access)),
      new DateOnly(2026, 9, 11),
      Now).Value;

    if (extended)
    {
      request.Extend(
        new ExtensionEntry(Ground: nameof(ExtensionGround.Complexity), Justification: "Quatre systèmes."),
        Now).IsSuccess.ShouldBeTrue();
    }

    if (closed)
    {
      SetStatus(request, RequestStatus.Completed);
    }

    return request;
  }

  private static void SetStatus(DataSubjectRequest request, RequestStatus status) =>
    typeof(DataSubjectRequest).GetProperty(nameof(DataSubjectRequest.Status))!.SetValue(request, status);
}
