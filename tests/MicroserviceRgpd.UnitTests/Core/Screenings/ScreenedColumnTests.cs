using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// L'invariant du contexte — <b>motif présent ⇔ ce n'est pas <c>Unflagged</c></b> — et la date de
/// l'arbitrage, qui n'a pas de chemin par lequel se dissocier de l'état qu'elle porte.
/// </summary>
public class ScreenedColumnTests
{
  private static readonly DateTimeOffset RenderedOn = new(2026, 8, 7, 14, 0, 0, TimeSpan.Zero);

  /// <summary>Une ligne signalée porte sa catégorie, le degré de la règle qui a déclenché, et son motif.</summary>
  [Fact]
  public void HoldsACategoryADegreeAndAReasonOnAFlaggedColumn()
  {
    var column = AScreening.AFlaggedColumn();

    column.Category.ShouldBe(PersonalDataCategory.ContactDetails);
    column.Strength.ShouldBe(RuleStrength.Morphological);
    column.Reason.ShouldBe("préfixe « adr » reconnu dans « adr_l1 »");
    column.IsFlagged.ShouldBeTrue();
  }

  /// <summary>
  /// La moitié droite de l'invariant : une ligne où rien n'a été vu n'a <b>ni</b> motif <b>ni</b>
  /// degré. Il n'y a rien à motiver, et il n'y a pas de règle à nommer — pas un degré faible, pas de
  /// règle du tout.
  /// </summary>
  [Fact]
  public void LeavesBothTheReasonAndTheDegreeAbsentWhenNothingWasSeen()
  {
    var column = ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh"));

    column.Category.ShouldBe(PersonalDataCategory.Unflagged);
    column.Reason.ShouldBeNull();
    column.Strength.ShouldBeNull();
    column.IsFlagged.ShouldBeFalse();
  }

  /// <summary>
  /// <b>La raison d'absence d'aperçu est une propriété de la ligne</b>, et la seule chose d'un
  /// <c>ColumnPreview</c> qui lui parvienne. Sans elle, les quatre comptes de la clause seraient
  /// incalculables une heure après le scan, quand les aperçus ont expiré — et l'écran d'archive
  /// deviendrait <b>plus rassurant</b> que celui du jour même.
  /// </summary>
  [Fact]
  public void CarriesWhyNoPreviewAccompaniedIt()
  {
    ScreenedColumn
      .NothingSeen(AScreening.AListedColumn("photo"), PreviewAbsenceReason.UnsampleableType)
      .PreviewAbsence.ShouldBe(PreviewAbsenceReason.UnsampleableType);

    ScreenedColumn.Flagged(
      AScreening.AListedColumn("iban"),
      PersonalDataCategory.FinancialData,
      RuleStrength.ExactName,
      "le nom entier figure au lexique",
      PreviewAbsenceReason.AccessDenied)
      .PreviewAbsence.ShouldBe(PreviewAbsenceReason.AccessDenied);
  }

  /// <summary>
  /// <b>Elle est nulle quand la question ne se pose pas</b> : le relevé a été collé, et rien n'a
  /// jamais été prélevé. Les quatre comptes de la clause y sont <b>absents</b>, jamais à zéro.
  /// </summary>
  [Fact]
  public void LeavesTheAbsenceReasonNullWhenNothingWasEverSampled()
  {
    ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh")).PreviewAbsence.ShouldBeNull();
    AScreening.AFlaggedColumn().PreviewAbsence.ShouldBeNull();
  }

  /// <summary>
  /// ⚠️ <b>Une absence d'aperçu n'est pas un signalement</b>, et elle ne dit rien de ce que la
  /// colonne porte : une ligne où rien n'a été vu peut parfaitement en porter une.
  /// </summary>
  [Fact]
  public void DoesNotFlagAColumnBecauseItsPreviewIsMissing()
  {
    var column = ScreenedColumn.NothingSeen(
      AScreening.AListedColumn("ref_3"),
      PreviewAbsenceReason.ReadFailed);

    column.IsFlagged.ShouldBeFalse();
    column.Category.ShouldBe(PersonalDataCategory.Unflagged);
    column.IsWithinReachOfABatchGesture.ShouldBeTrue();
  }

  /// <summary>
  /// ⚠️ <b>Aucun chemin ne pose <c>Unflagged</c> avec un motif.</b> Une ligne « rien vu » qui se
  /// justifierait serait une ligne « vue et écartée » déguisée, et c'est exactement la distinction
  /// que <c>PersonalDataUncategorised</c> existe pour tenir.
  /// </summary>
  [Fact]
  public void RefusesToFlagAColumnUnderTheUnflaggedValue()
  {
    Should.Throw<ArgumentException>(() => ScreenedColumn.Flagged(
      AScreening.AListedColumn("id_adh"),
      PersonalDataCategory.Unflagged,
      RuleStrength.ExactName,
      "quelque chose"));
  }

  /// <summary>
  /// ⚠️ <b>Et aucun ne signale sans motif.</b> Une réserve sans motif est une panne du contrat, pas
  /// une réserve : « <c>ContactDetails</c>, 0,72 » ne s'arbitre pas.
  /// </summary>
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void RefusesAFlaggedColumnWithoutAReasonAnOperatorCouldRead(string? reason)
  {
    Should.Throw<ArgumentException>(() => ScreenedColumn.Flagged(
      AScreening.AListedColumn("adr_l1"),
      PersonalDataCategory.ContactDetails,
      RuleStrength.Morphological,
      reason));
  }

  /// <summary>
  /// Le cas d'usage le plus fort du repli : un conteneur libre est <b>vu</b>, il n'est pas
  /// « rien vu ». Le motif est rédigeable, donc ce n'est pas <c>Unflagged</c>.
  /// </summary>
  [Fact]
  public void FlagsAFreeContainerAsUncategorisedRatherThanAsNothingSeen()
  {
    var column = ScreenedColumn.Flagged(
      AScreening.AListedColumn("cfdata", dataType: "jsonb"),
      PersonalDataCategory.PersonalDataUncategorised,
      RuleStrength.TypeHeuristic,
      "conteneur libre : le contenu n'est pas lisible depuis le schéma");

    column.Category.ShouldBe(PersonalDataCategory.PersonalDataUncategorised);
    column.IsFlagged.ShouldBeTrue();
  }

  /// <summary>L'invariant, énoncé tel quel sur les deux chemins de naissance qui existent.</summary>
  [Fact]
  public void CarriesAReasonIfAndOnlyIfItIsNotUnflagged()
  {
    ScreenedColumn[] columns =
    [
      AScreening.AFlaggedColumn(),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh")),
    ];

    columns
      .Select(column => (CarriesAReason: column.Reason != null, IsFlagged: column.Category != PersonalDataCategory.Unflagged))
      .ShouldAllBe(line => line.CarriesAReason == line.IsFlagged);
  }

  /// <summary>Née <c>Awaiting</c>, et sans arbitrage : c'est le seul état qui n'en a pas.</summary>
  [Fact]
  public void IsBornAwaitingWithNoArbitrationAtAll()
  {
    var column = AScreening.AFlaggedColumn();

    column.State.ShouldBe(ScreenedColumnState.Awaiting);
    column.Arbitration.ShouldBeNull();
    column.AwaitsAnArbitration.ShouldBeTrue();
  }

  /// <summary>
  /// <c>Awaiting</c> n'est pas une issue : personne ne tranche une absence de décision, et le
  /// service ne tranche jamais à la place de l'<c>Operator</c>.
  /// </summary>
  [Fact]
  public void RefusesAwaitingAsARulingSinceNoOneEverRulesAnAbsenceOfDecision()
  {
    Should.Throw<ArgumentException>(
      () => Arbitration.Rendered(ScreenedColumnState.Awaiting, RenderedOn));
  }

  /// <summary>Une issue rendue porte l'état et la date — les deux, toujours ensemble.</summary>
  [Fact]
  public void CarriesTheStateAndTheDateTogetherOnceAHumanHasRuled()
  {
    var screening = AScreening.Of(AScreening.AFlaggedColumn());
    var identity = ColumnIdentity.Of("public", "adherents", "adr_l1");

    var arbitrated = screening.Arbitrate(identity, ScreenedColumnState.Retained, RenderedOn);

    arbitrated.ShouldNotBeNull();
    arbitrated.State.ShouldBe(ScreenedColumnState.Retained);
    arbitrated.Arbitration!.RenderedOn.ShouldBe(RenderedOn);
    arbitrated.AwaitsAnArbitration.ShouldBeFalse();
  }

  /// <summary>
  /// ⚠️ <b>Une colonne <c>Unflagged</c> est arbitrable comme les autres</b>, et c'est ce qui paye le
  /// fait de toutes les rendre. Un <c>Retained</c> posé là prouve qu'un <b>humain</b> l'a retenue,
  /// jamais que le service l'avait vue.
  /// </summary>
  [Fact]
  public void LetsAnOperatorRetainAColumnWhereTheServiceSawNothing()
  {
    var screening = AScreening.Of(ScreenedColumn.NothingSeen(AScreening.AListedColumn("livret_modaccomp_code")));
    var identity = ColumnIdentity.Of("public", "adherents", "livret_modaccomp_code");

    var arbitrated = screening.Arbitrate(identity, ScreenedColumnState.Retained, RenderedOn);

    arbitrated!.State.ShouldBe(ScreenedColumnState.Retained);
    arbitrated.Category.ShouldBe(PersonalDataCategory.Unflagged);
    arbitrated.Reason.ShouldBeNull();
  }

  /// <summary>
  /// <b>Un second arbitrage écrase le premier</b>, et le coût est déclaré : la date de celui qu'il
  /// remplace est effacée. Aucun journal de preuve n'en garde la trace ici.
  /// </summary>
  [Fact]
  public void OverwritesTheArbitrationWhenAHumanChangesHisMind()
  {
    var screening = AScreening.Of(AScreening.AFlaggedColumn());
    var identity = ColumnIdentity.Of("public", "adherents", "adr_l1");

    screening.Arbitrate(identity, ScreenedColumnState.Retained, RenderedOn);
    var arbitrated = screening.Arbitrate(identity, ScreenedColumnState.SetAside, RenderedOn.AddDays(1));

    arbitrated!.State.ShouldBe(ScreenedColumnState.SetAside);
    arbitrated.Arbitration!.RenderedOn.ShouldBe(RenderedOn.AddDays(1));
  }

  /// <summary>
  /// L'état n'est pas un champ : c'est un calcul sur la présence de l'arbitrage, et il ne peut donc
  /// pas s'en dissocier.
  /// </summary>
  [Fact]
  public void OffersNoSettableStateBesideTheArbitrationThatCarriesIt()
  {
    typeof(ScreenedColumn)
      .GetProperty(nameof(ScreenedColumn.State))!
      .CanWrite
      .ShouldBeFalse();
  }

  /// <summary>
  /// ⚠️ <b>Ce que le geste de lot atteint est écrit ici, sur la ligne, et à un seul endroit.</b> Une
  /// ligne signalée n'est jamais à sa portée — <c>une suspicion ne s'écarte jamais sans avoir été lue
  /// une par une</c> — et une ligne déjà tranchée non plus : le lot liquide ce qui attend, il
  /// n'efface pas ce qu'un humain avait dit. Poser la règle dans une requête l'aurait rendue
  /// invisible à qui lit le domaine, là où c'est très exactement une règle du domaine.
  /// </summary>
  [Theory]
  [InlineData(false, false, true)]
  [InlineData(true, false, false)]
  [InlineData(false, true, false)]
  [InlineData(true, true, false)]
  public void SaysWhetherABatchGestureMayReachItAndOnlyEverReachesAnUnflaggedColumnStillAwaiting(
    bool flagged,
    bool alreadyArbitrated,
    bool withinReach)
  {
    var screening = AScreening.Of(flagged
      ? AScreening.AFlaggedColumn("adr_l1")
      : ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh")));

    var identity = ColumnIdentity.Of("public", "adherents", flagged ? "adr_l1" : "id_adh");

    if (alreadyArbitrated)
    {
      screening.Arbitrate(identity, ScreenedColumnState.Retained, RenderedOn);
    }

    screening.ColumnAt(identity)
      .ShouldNotBeNull()
      .IsWithinReachOfABatchGesture
      .ShouldBe(withinReach);
  }
}
