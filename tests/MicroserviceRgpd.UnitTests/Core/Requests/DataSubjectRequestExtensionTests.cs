using Ardalis.Result;
using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UnitTests.Core.Requests;

/// <summary>
/// <b>Prolonger une demande</b> : la date limite de réponse est reportée de deux mois, la date limite
/// en vigueur est d'abord recopiée, et les quatre valeurs de la prolongation sont posées ensemble
/// (ADR-0029).
/// </summary>
/// <remarks>
/// ⚠️ <b>L'arithmétique des dates est celle de <see cref="DateOnly.AddMonths"/></b>, comme la règle
/// de l'ADR-0021 : un jour qui n'existe pas dans le mois d'arrivée se replie sur son dernier jour.
/// C'est la table que ces tests tiennent — le navigateur, lui, ne calcule aucune date.
/// </remarks>
public class DataSubjectRequestExtensionTests
{
  private static readonly DateOnly Today = new(2026, 9, 11);

  private static readonly DateTimeOffset Now = new(2026, 9, 11, 8, 15, 0, TimeSpan.Zero);

  /// <summary>L'instant de la prolongation, postérieur à l'enregistrement.</summary>
  private static readonly DateTimeOffset Later = new(2026, 9, 11, 17, 40, 0, TimeSpan.Zero);

  /// <summary>Une saisie complète et valide, que chaque test déforme sur un seul point.</summary>
  private static ExtensionEntry AValidExtension() => new(
    Ground: "Complexity",
    Justification: "Les données de la personne sont réparties sur quatre systèmes.");

  /// <summary>
  /// <b>La table d'arithmétique des dates</b>, sur la règle elle-même : deux mois de plus, repliés
  /// sur le dernier jour du mois quand le jour n'y existe pas. ⚠️ <c>Date.setMonth</c> rendrait le
  /// 3 mars là où le domaine rend le 28 février.
  /// </summary>
  [Theory]
  [InlineData("2026-08-31", "2026-10-31")]
  [InlineData("2026-12-31", "2027-02-28")]
  [InlineData("2027-12-31", "2028-02-29")]
  [InlineData("2026-01-15", "2026-03-15")]
  [InlineData("2026-12-30", "2027-02-28")]
  public void ReportsTheDeadlineByTwoMonthsWithTheEndOfMonthFallback(string deadline, string extended)
  {
    DataSubjectRequest.DeadlineExtendedFrom(DateOnly.Parse(deadline, System.Globalization.CultureInfo.InvariantCulture))
      .ShouldBe(DateOnly.Parse(extended, System.Globalization.CultureInfo.InvariantCulture));
  }

  /// <summary>
  /// <b>La même table, tenue par le geste</b> : la date limite en vigueur part vers
  /// <see cref="DataSubjectRequest.InitialResponseDeadline"/> <b>intacte</b>, et
  /// <see cref="DataSubjectRequest.ResponseDeadline"/> prend les deux mois.
  /// </summary>
  [Theory]
  [InlineData("2026-08-31", "2026-10-31")]
  [InlineData("2026-12-31", "2027-02-28")]
  [InlineData("2027-12-31", "2028-02-29")]
  public void KeepsTheInitialDeadlineIntactAndReportsTheOneInForce(string deadline, string extended)
  {
    var initial = DateOnly.Parse(deadline, System.Globalization.CultureInfo.InvariantCulture);
    var request = ARequestWithDeadline(initial);

    request.Extend(AValidExtension(), Today, Later).IsSuccess.ShouldBeTrue();

    request.InitialResponseDeadline.ShouldBe(initial);
    request.ResponseDeadline.ShouldBe(DateOnly.Parse(extended, System.Globalization.CultureInfo.InvariantCulture));
  }

  /// <summary>
  /// <b>Les quatre valeurs sont posées ensemble</b>, et le statut ne bouge pas : une demande
  /// prolongée reste En cours.
  /// </summary>
  [Fact]
  public void PostsTheFourValuesOfTheExtensionTogether()
  {
    var request = ARequest();

    request.Extend(AValidExtension() with { Ground = "NumberOfRequests" }, Today, Later).IsSuccess.ShouldBeTrue();

    request.Extended.ShouldBeTrue();
    request.InitialResponseDeadline.ShouldBe(new DateOnly(2026, 10, 10));
    request.ExtensionGround.ShouldBe(ExtensionGround.NumberOfRequests);
    request.ExtensionJustification!.Value.Value.ShouldBe("Les données de la personne sont réparties sur quatre systèmes.");
    request.ExtendedAt.ShouldBe(Later);
    request.Status.ShouldBe(RequestStatus.InProgress);
  }

  /// <summary>⚠️ <b>Une demande qui n'a pas été prolongée ne porte aucune des quatre valeurs.</b></summary>
  [Fact]
  public void CarriesNoExtensionUntilItIsExtended()
  {
    var request = ARequest();

    request.Extended.ShouldBeFalse();
    request.InitialResponseDeadline.ShouldBeNull();
    request.ExtensionGround.ShouldBeNull();
    request.ExtensionJustification.ShouldBeNull();
    request.ExtendedAt.ShouldBeNull();
  }

  /// <summary>L'instant de la prolongation est ramené en UTC, comme les autres empreintes.</summary>
  [Fact]
  public void StampsTheExtensionInUtc()
  {
    var request = ARequest();

    request.Extend(AValidExtension(), Today, new DateTimeOffset(2026, 9, 11, 19, 40, 0, TimeSpan.FromHours(2)))
      .IsSuccess.ShouldBeTrue();

    request.ExtendedAt.ShouldBe(new DateTimeOffset(2026, 9, 11, 17, 40, 0, TimeSpan.Zero));
    request.ExtendedAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
  }

  /// <summary>La justification est élaguée avant d'être tenue, comme le message d'une demande.</summary>
  [Fact]
  public void TrimsTheJustification()
  {
    var request = ARequest();

    request.Extend(AValidExtension() with { Justification = "  Quatre systèmes.  " }, Today, Later)
      .IsSuccess.ShouldBeTrue();

    request.ExtensionJustification!.Value.Value.ShouldBe("Quatre systèmes.");
  }

  /// <summary>
  /// <b>Une saisie fautive ne prolonge rien</b> : le refus est rattaché à son champ, et la demande
  /// reste telle quelle — date limite comprise.
  /// </summary>
  [Fact]
  public void RefusesAFaultyEntryWithoutTouchingTheRequest()
  {
    var request = ARequest();

    var result = request.Extend(new ExtensionEntry(Ground: "Autre", Justification: "   "), Today, Later);

    result.Status.ShouldBe(ResultStatus.Invalid);
    result.ValidationErrors.Select(error => error.Identifier).ShouldBe(
      [DataSubjectRequestField.ExtensionGround, DataSubjectRequestField.ExtensionJustification],
      ignoreOrder: true);
    request.Extended.ShouldBeFalse();
    request.ResponseDeadline.ShouldBe(new DateOnly(2026, 10, 10));
  }

  /// <summary>Une demande valide, reçue le 10 septembre 2026 : sa date limite est le 10 octobre.</summary>
  private static DataSubjectRequest ARequest() =>
    DataSubjectRequest.Receive(
      new DataSubjectRequestEntry(
        Origin: Origin.Email,
        ReceivedOn: "2026-09-10",
        LastName: "Dupont",
        FirstName: "Jeanne",
        Email: "jeanne.dupont@exemple.fr",
        IdentityVerified: false,
        Message: "Je souhaite accéder à mes données.",
        Right: "Access"),
      Today,
      Now).Value;

  /// <summary>
  /// Une demande dont la date limite de réponse est <paramref name="deadline"/>. ⚠️ <b>La date se
  /// pose par réflexion</b> : aucune date de réception ne produit un 31 décembre, que la table doit
  /// pourtant éprouver — la réception recule d'un mois, et novembre n'a pas de 31.
  /// </summary>
  private static DataSubjectRequest ARequestWithDeadline(DateOnly deadline)
  {
    var request = ARequest();

    typeof(DataSubjectRequest)
      .GetProperty(nameof(DataSubjectRequest.ResponseDeadline))!
      .SetValue(request, deadline);

    return request;
  }
}
