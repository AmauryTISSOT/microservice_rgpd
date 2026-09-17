using System.Globalization;
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
  private static readonly DateOnly ReceptionDay = new(2026, 9, 11);

  private static readonly DateTimeOffset Now = new(2026, 9, 11, 8, 15, 0, TimeSpan.Zero);

  /// <summary>L'instant de la prolongation, postérieur à l'enregistrement.</summary>
  private static readonly DateTimeOffset Later = new(2026, 9, 11, 17, 40, 0, TimeSpan.Zero);

  /// <summary>L'instant d'une correction, postérieur à la prolongation.</summary>
  private static readonly DateTimeOffset MuchLater = new(2026, 9, 12, 9, 5, 0, TimeSpan.Zero);

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
    DataSubjectRequest.DeadlineExtendedFrom(DateOnly.Parse(deadline, CultureInfo.InvariantCulture))
      .ShouldBe(DateOnly.Parse(extended, CultureInfo.InvariantCulture));
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
    var initial = DateOnly.Parse(deadline, CultureInfo.InvariantCulture);
    var request = ARequestWithDeadline(initial);

    request.Extend(AValidExtension(), Later).IsSuccess.ShouldBeTrue();

    request.InitialResponseDeadline.ShouldBe(initial);
    request.ResponseDeadline.ShouldBe(DateOnly.Parse(extended, CultureInfo.InvariantCulture));
  }

  /// <summary>
  /// <b>Les quatre valeurs sont posées ensemble</b>, et le statut ne bouge pas : une demande
  /// prolongée reste En cours.
  /// </summary>
  [Fact]
  public void PostsTheFourValuesOfTheExtensionTogether()
  {
    var request = ARequest();

    request.Extend(AValidExtension() with { Ground = "NumberOfRequests" }, Later).IsSuccess.ShouldBeTrue();

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

    request.Extend(AValidExtension(), new DateTimeOffset(2026, 9, 11, 19, 40, 0, TimeSpan.FromHours(2)))
      .IsSuccess.ShouldBeTrue();

    request.ExtendedAt.ShouldBe(new DateTimeOffset(2026, 9, 11, 17, 40, 0, TimeSpan.Zero));
    request.ExtendedAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
  }

  /// <summary>La justification est élaguée avant d'être tenue, comme le message d'une demande.</summary>
  [Fact]
  public void TrimsTheJustification()
  {
    var request = ARequest();

    request.Extend(AValidExtension() with { Justification = "  Quatre systèmes.  " }, Later)
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

    var result = request.Extend(new ExtensionEntry(Ground: "Autre", Justification: "   "), Later);

    result.Status.ShouldBe(ResultStatus.Invalid);
    result.ValidationErrors.Select(error => error.Identifier).ShouldBe(
      [DataSubjectRequestField.ExtensionGround, DataSubjectRequestField.ExtensionJustification],
      ignoreOrder: true);
    request.Extended.ShouldBeFalse();
    request.ResponseDeadline.ShouldBe(new DateOnly(2026, 10, 10));
  }

  /// <summary>
  /// <b>Un motif absent, blanc ou forgé est refusé sous le champ motif</b>, sous le message que le
  /// serveur a écrit une seule fois. ⚠️ <b>Le choix fermé de l'écran ne suffit pas</b> : un envoi
  /// forgé n'en vient pas, et le domaine revérifie — <c>Autre</c> n'est pas un motif.
  /// </summary>
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("Autre")]
  [InlineData("complexity")]
  [InlineData("Complexité de la demande")]
  [InlineData("0")]
  public void RefusesAGroundThatIsNotOneOfTheTwo(string? ground)
  {
    var request = ARequest();

    var result = request.Extend(AValidExtension() with { Ground = ground }, Later);

    RefusalsOf(result).ShouldBe(
      [$"{DataSubjectRequestField.ExtensionGround} : {DataSubjectRequestMessages.ExtensionGroundMissing}"]);
    request.Extended.ShouldBeFalse();
  }

  /// <summary>
  /// <b>Une justification absente, vide ou blanche après élagage est refusée sous son champ</b> : le
  /// texte qui dit le fait concret est obligatoire.
  /// </summary>
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData(" \t\n ")]
  public void RefusesAJustificationThatSaysNothing(string? justification)
  {
    var request = ARequest();

    var result = request.Extend(AValidExtension() with { Justification = justification }, Later);

    RefusalsOf(result).ShouldBe(
      [$"{DataSubjectRequestField.ExtensionJustification} : {DataSubjectRequestMessages.ExtensionJustificationMissing}"]);
    request.Extended.ShouldBeFalse();
  }

  /// <summary>
  /// <b>Un caractère au-delà du plafond suffit</b> : deux mille un est refusé, deux mille passe. Le
  /// domaine ne tronque rien, il refuse — et les bordures ne comptent pas, elles sont élaguées avant.
  /// </summary>
  [Fact]
  public void RefusesAJustificationOverItsCeiling()
  {
    var request = ARequest();

    var result = request.Extend(
      AValidExtension() with { Justification = new string('j', ExtensionJustification.MaxLength + 1) },
      Later);

    RefusalsOf(result).ShouldBe(
      [$"{DataSubjectRequestField.ExtensionJustification} : {DataSubjectRequestMessages.ExtensionJustificationTooLong}"]);
    request.Extended.ShouldBeFalse();

    request.Extend(
      AValidExtension() with { Justification = $"  {new string('j', ExtensionJustification.MaxLength)}  " },
      Later).IsSuccess.ShouldBeTrue("Une justification au plafond exact est refusée.");
  }

  /// <summary>
  /// ⚠️ <b>Le plafond que la phrase dit en toutes lettres est celui de l'objet valeur</b> : « 2 000 »
  /// n'est pas interpolé, et rien ne les tiendrait ensemble sans ce test.
  /// </summary>
  [Fact]
  public void SaysTheCeilingItEnforces()
  {
    // Le plafond tel que la phrase l'écrit : les milliers séparés d'une espace, comme le français
    // les écrit — et une espace ordinaire, celle que le message porte.
    var written = ExtensionJustification.MaxLength
      .ToString("#,##0", CultureInfo.InvariantCulture)
      .Replace(",", " ", StringComparison.Ordinal);

    DataSubjectRequestMessages.ExtensionJustificationTooLong.ShouldContain(written);
  }

  // ─── Modifier une demande prolongée ─────────────────────────────────────────────────────────

  /// <summary>
  /// <b>Corriger la date de réception refait les deux dates limites ensemble</b> : la date limite
  /// initiale repart de la réception corrigée, et celle en vigueur reprend ses deux mois par-dessus.
  /// ⚠️ <b>L'écart de la prolongation est conservé</b> — la correction ne l'annule pas.
  /// </summary>
  [Theory]
  [InlineData("2026-03-31", "2026-04-30", "2026-06-30")]
  [InlineData("2026-12-31", "2027-01-31", "2027-03-31")]
  [InlineData("2027-01-31", "2027-02-28", "2027-04-28")]
  [InlineData("2026-01-15", "2026-02-15", "2026-04-15")]
  public void RecomputesBothDeadlinesFromTheCorrectedReceptionDate(
    string receivedOn,
    string initial,
    string inForce)
  {
    var request = AnExtendedRequest();

    // Chaque correction se pose le jour même de la réception corrigée, pour que les dates encore à
    // venir restent admises.
    var todayInParis = DateOnly.Parse(receivedOn, CultureInfo.InvariantCulture);

    request.Modify(AnEntryReceivedOn(receivedOn), todayInParis, MuchLater).IsSuccess.ShouldBeTrue();

    request.InitialResponseDeadline.ShouldBe(DateOnly.Parse(initial, CultureInfo.InvariantCulture));
    request.ResponseDeadline.ShouldBe(DateOnly.Parse(inForce, CultureInfo.InvariantCulture));
  }

  /// <summary>
  /// ⚠️ <b>La correction ne touche ni le motif, ni la justification, ni l'instant du geste</b> :
  /// corriger une erreur de saisie n'annule pas une prolongation — elle reste ce qu'elle a été.
  /// </summary>
  [Fact]
  public void LeavesTheGroundTheJustificationAndTheExtensionStampUntouched()
  {
    var request = AnExtendedRequest();

    request.Modify(AnEntryReceivedOn("2026-03-31"), new DateOnly(2026, 3, 31), MuchLater)
      .IsSuccess.ShouldBeTrue();

    request.Extended.ShouldBeTrue();
    request.ExtensionGround.ShouldBe(ExtensionGround.Complexity);
    request.ExtensionJustification!.Value.Value.ShouldBe(
      "Les données de la personne sont réparties sur quatre systèmes.");
    request.ExtendedAt.ShouldBe(Later);
  }

  /// <summary>
  /// ⚠️ <b>Une modification qui ne change rien ne touche aucune des deux dates</b> : la règle ne vaut
  /// que sur le chemin d'écriture. Sans cela, un renvoi à l'identique rejouerait le calcul, et
  /// écraserait une date limite posée ailleurs.
  /// </summary>
  [Fact]
  public void TouchesNeitherDeadlineWhenTheModificationChangesNothing()
  {
    var request = AnExtendedRequestWithDeadline(new DateOnly(2026, 12, 31));
    var initial = request.InitialResponseDeadline;
    var inForce = request.ResponseDeadline;

    request.Modify(AnEntryReceivedOn("2026-09-10"), ReceptionDay, MuchLater).IsSuccess.ShouldBeTrue();

    request.InitialResponseDeadline.ShouldBe(initial);
    request.ResponseDeadline.ShouldBe(inForce);
    request.ModifiedAt.ShouldBeNull();
  }

  /// <summary>
  /// <b>Sur une demande qui n'a jamais été prolongée, la modification se comporte comme avant</b> :
  /// la seule date limite suit la réception corrigée, et la date limite initiale reste vide.
  /// </summary>
  [Fact]
  public void LeavesTheInitialDeadlineEmptyOnARequestThatWasNeverExtended()
  {
    var request = ARequest();

    request.Modify(AnEntryReceivedOn("2026-03-31"), new DateOnly(2026, 3, 31), MuchLater)
      .IsSuccess.ShouldBeTrue();

    request.ResponseDeadline.ShouldBe(new DateOnly(2026, 4, 30));
    request.InitialResponseDeadline.ShouldBeNull();
    request.Extended.ShouldBeFalse();
  }

  /// <summary>
  /// <b>Une correction qui place rétroactivement la prolongation hors de sa fenêtre est acceptée</b> :
  /// c'est une vérité à afficher, pas un état à empêcher — aucune erreur de saisie n'est
  /// indéracinable. La demande reste prolongée, et ne se prolonge toujours pas une seconde fois.
  /// </summary>
  [Fact]
  public void AcceptsACorrectionThatPutsTheExtendedRequestRetroactivelyLate()
  {
    var request = AnExtendedRequest();

    var result = request.Modify(AnEntryReceivedOn("2025-01-15"), ReceptionDay, MuchLater);

    result.IsSuccess.ShouldBeTrue();
    request.InitialResponseDeadline.ShouldBe(new DateOnly(2025, 2, 15));
    request.ResponseDeadline.ShouldBe(new DateOnly(2025, 4, 15));
    request.ResponseDeadline.ShouldBeLessThan(ReceptionDay, "La correction n'a pas mis la demande en retard.");
    request.Extended.ShouldBeTrue();
    request.ExtensionBlockFacing(ReceptionDay).ShouldBe(ExtensionBlock.AlreadyExtended);
  }

  /// <summary>Les refus d'un résultat, sous la forme <c>champ : message</c>.</summary>
  private static string[] RefusalsOf(Result result)
  {
    result.Status.ShouldBe(ResultStatus.Invalid);

    return [.. result.ValidationErrors.Select(error => $"{error.Identifier} : {error.ErrorMessage}")];
  }

  /// <summary>Une demande valide, reçue le 10 septembre 2026 : sa date limite est le 10 octobre.</summary>
  private static DataSubjectRequest ARequest() =>
    DataSubjectRequest.Receive(AnEntryReceivedOn("2026-09-10"), ReceptionDay, Now).Value;

  /// <summary>
  /// Une demande dont la date limite de réponse est <paramref name="deadline"/>. ⚠️ <b>La date se
  /// pose par réflexion</b> : aucune date de réception ne produit un 31 décembre, que la table doit
  /// pourtant éprouver — la réception recule d'un mois, et novembre n'a pas de 31.
  /// </summary>
  private static DataSubjectRequest ARequestWithDeadline(DateOnly deadline) =>
    WithDeadline(ARequest(), deadline);

  /// <summary>
  /// Une demande <b>prolongée</b>, reçue le 10 septembre 2026 : sa date limite initiale est le
  /// 10 octobre, et celle en vigueur le 10 décembre.
  /// </summary>
  private static DataSubjectRequest AnExtendedRequest()
  {
    var request = ARequest();

    request.Extend(AValidExtension(), Later).IsSuccess.ShouldBeTrue();

    return request;
  }

  /// <summary>
  /// Une demande prolongée dont la date limite en vigueur est <paramref name="deadline"/> — posée
  /// par réflexion, comme <see cref="ARequestWithDeadline"/>, pour éprouver qu'un renvoi à
  /// l'identique ne la rejoue pas.
  /// </summary>
  private static DataSubjectRequest AnExtendedRequestWithDeadline(DateOnly deadline) =>
    WithDeadline(AnExtendedRequest(), deadline);

  /// <summary>
  /// <paramref name="request"/>, sa date limite de réponse posée <b>par réflexion</b> — la seule
  /// manière d'atteindre une date qu'aucune date de réception ne produit.
  /// </summary>
  private static DataSubjectRequest WithDeadline(DataSubjectRequest request, DateOnly deadline)
  {
    typeof(DataSubjectRequest)
      .GetProperty(nameof(DataSubjectRequest.ResponseDeadline))!
      .SetValue(request, deadline);

    return request;
  }

  /// <summary>
  /// La saisie de <see cref="ARequest"/>, sa seule date de réception changée : tout le reste à
  /// l'identique, pour qu'une correction ne porte que sur elle.
  /// </summary>
  private static DataSubjectRequestEntry AnEntryReceivedOn(string receivedOn) => new(
    Origin: Origin.Email,
    ReceivedOn: receivedOn,
    LastName: "Dupont",
    FirstName: "Jeanne",
    Email: "jeanne.dupont@exemple.fr",
    IdentityVerified: false,
    Message: "Je souhaite accéder à mes données.",
    Right: "Access");
}
