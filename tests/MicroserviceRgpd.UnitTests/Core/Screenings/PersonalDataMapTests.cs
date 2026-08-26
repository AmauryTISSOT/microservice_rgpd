using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// Ce que la <c>Cartographie</c> porte, et surtout <b>ce qu'elle ne porte pas</b>.
/// </summary>
public class PersonalDataMapTests
{
  private static readonly DateTimeOffset ExportedOn =
    new(2026, 8, 26, 14, 5, 30, TimeSpan.FromHours(2));

  /// <summary>
  /// ⚠️ <b>Toutes les lignes, sans exception.</b> Une cartographie réduite aux retenues serait le
  /// filtre que l'<c>Omission relue</c> interdit, déplacé du rapport vers l'export : elle se lirait
  /// comme la liste <b>complète</b> des données personnelles du client.
  /// </summary>
  [Fact]
  public void CarriesEveryColumnIncludingTheUnflaggedAndTheSetAside()
  {
    var flagged = AScreening.AFlaggedColumn("adr_l1", position: 1);
    var unflagged = ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh", position: 2));
    var alsoUnflagged = ScreenedColumn.NothingSeen(AScreening.AListedColumn("cotisation", position: 3));

    var screening = AScreening.Of(flagged, unflagged, alsoUnflagged);

    screening.Arbitrate(flagged.Identity, ScreenedColumnState.Retained, ExportedOn);
    screening.Arbitrate(alsoUnflagged.Identity, ScreenedColumnState.SetAside, ExportedOn);

    var map = PersonalDataMap.Of(screening, ExportedOn);

    map.Columns.Count.ShouldBe(3);
    map.Columns.Select(column => column.Column)
      .ShouldBe(["adr_l1", "id_adh", "cotisation"]);
    map.Columns.Select(column => column.State)
      .ShouldBe(["retenue", "à arbitrer", "écartée"]);
  }

  /// <summary>
  /// L'absence de signalement est une catégorie écrite, jamais une case laissée vide : sans elle,
  /// une ligne non signalée se lirait comme une ligne oubliée.
  /// </summary>
  [Fact]
  public void WritesUnflaggedAsACategoryRatherThanAsAnAbsence()
  {
    var map = PersonalDataMap.Of(
      AScreening.Of(ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh"))),
      ExportedOn);

    var line = map.Columns.Single();

    line.IsFlagged.ShouldBeFalse();
    line.Category.ShouldBe(PersonalDataCategory.Unflagged.Name);
    line.CategoryLabel.ShouldBe(PersonalDataCategory.Unflagged.FrenchLabel);
    line.Strength.ShouldBeNull();
    line.StrengthLabel.ShouldBeNull();
    line.Reason.ShouldBeNull();
  }

  /// <summary>
  /// ⚠️ <b>Les deux paires nom canonique / libellé français voyagent ensemble</b> : l'anglais se
  /// traite, le français se lit, et n'en garder qu'un force à choisir entre deux destinataires qui
  /// existent tous les deux.
  /// </summary>
  [Fact]
  public void KeepsTheCanonicalNameAndTheFrenchLabelSideBySide()
  {
    var flagged = AScreening.AFlaggedColumn();

    var line = PersonalDataMap.Of(AScreening.Of(flagged), ExportedOn).Columns.Single();

    line.Category.ShouldBe(PersonalDataCategory.ContactDetails.Name);
    line.CategoryLabel.ShouldBe(PersonalDataCategory.ContactDetails.FrenchLabel);
    line.Strength.ShouldBe(RuleStrength.Morphological.Name);
    line.StrengthLabel.ShouldBe(RuleStrength.Morphological.FrenchLabel);
    line.Reason.ShouldNotBeNullOrWhiteSpace();
  }

  /// <summary>
  /// ⚠️ <b>La date d'arbitrage est celle du <c>RenderedOn</c> de l'ADR-0014</b>, et rien ne dit qui a
  /// tranché : ce contexte ne l'enregistre pas.
  /// </summary>
  [Fact]
  public void CarriesTheDateOfTheRulingAndNothingOfWhoRenderedIt()
  {
    var column = AScreening.AFlaggedColumn();
    var screening = AScreening.Of(column);
    var renderedOn = new DateTimeOffset(2026, 8, 20, 11, 0, 0, TimeSpan.FromHours(2));

    screening.Arbitrate(column.Identity, ScreenedColumnState.Retained, renderedOn);

    var line = PersonalDataMap.Of(screening, ExportedOn).Columns.Single();

    line.RenderedOn.ShouldBe(renderedOn);

    // Le type de la ligne n'a pas de champ pour un signataire, et c'est ce qui doit rester vrai.
    typeof(MappedColumn).GetProperties()
      .Select(property => property.Name)
      .ShouldNotContain(name => name.Contains("Sign", StringComparison.OrdinalIgnoreCase));
  }

  /// <summary>Rien n'a été tranché : la case reste vide, et l'état le dit en toutes lettres.</summary>
  [Fact]
  public void LeavesTheDateEmptyWhileNobodyHasRuled()
  {
    var line = PersonalDataMap.Of(AScreening.Of(AScreening.AFlaggedColumn()), ExportedOn)
      .Columns.Single();

    line.RenderedOn.ShouldBeNull();
    line.State.ShouldBe(ScreenedColumnState.Awaiting.FrenchLabel);
  }

  /// <summary>
  /// ⚠️ <b>Aucun champ ne porte une valeur lue, ni un compte de valeurs.</b> Le seul texte qui vient
  /// de la base est un commentaire de <b>catalogue</b>, jamais une donnée. Éprouvé par le type
  /// lui-même : il n'a aucun champ où une valeur pourrait se ranger.
  /// </summary>
  [Fact]
  public void HasNowhereToPutAReadValueNorACountOfThem()
  {
    var names = typeof(MappedColumn).GetProperties().Select(property => property.Name).ToArray();

    names.Length.ShouldBe(16, "La spec fixe seize champs par ligne, ni quinze ni dix-sept.");

    names.ShouldNotContain(name => name.Contains("Preview", StringComparison.OrdinalIgnoreCase));
    names.ShouldNotContain(name => name.Contains("Sample", StringComparison.OrdinalIgnoreCase));
    names.ShouldNotContain(name => name.Contains("Value", StringComparison.OrdinalIgnoreCase));
    names.ShouldNotContain(name => name.Contains("Conform", StringComparison.OrdinalIgnoreCase));

    typeof(PersonalDataMap).GetProperties()
      .Select(property => property.Name)
      .ShouldNotContain(name => name.Contains("Clause", StringComparison.OrdinalIgnoreCase));
  }

  /// <summary>
  /// ⚠️ <b>Vraie même sur un rapport <c>Scanné</c> aux aperçus vivants</b> : un
  /// <see cref="ColumnPreview"/> n'est atteignable ni depuis un rapport, ni depuis une de ses lignes,
  /// et rien dans la cartographie ne va le chercher ailleurs.
  /// </summary>
  [Fact]
  public void CarriesNoPreviewEvenWhenTheListingWasScanned()
  {
    var screening = AScreening.OfListing(
      ListingOrigin.Scanned,
      AScreening.AFlaggedColumn(),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh", position: 2)));

    var map = PersonalDataMap.Of(screening, ExportedOn);

    map.Columns.Count.ShouldBe(2);

    // Rien de ce que la cartographie expose n'ouvre un chemin vers un aperçu.
    typeof(ScreenedColumn).GetProperties()
      .Select(property => property.PropertyType)
      .ShouldNotContain(typeof(ColumnPreview));
  }

  /// <summary>
  /// L'ordre du fichier est celui de l'écran — tables retriées, colonnes dans l'ordre du schéma —
  /// sans quoi le fichier et l'écran d'où il sort deviennent incomparables.
  /// </summary>
  [Fact]
  public void OrdersTheRowsLikeTheScreenDoes()
  {
    var screening = AScreening.Of(
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("ville", table: "adresses", position: 2)),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh", table: "adherents", position: 2)),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("adr_l1", table: "adresses", position: 1)),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("nom", table: "adherents", position: 1)));

    PersonalDataMap.Of(screening, ExportedOn).Columns
      .Select(column => $"{column.Table}.{column.Column}")
      .ShouldBe(["adherents.nom", "adherents.id_adh", "adresses.adr_l1", "adresses.ville"]);
  }

  /// <summary>
  /// ⚠️ <b>L'arbitrage inachevé se compte à part, en une phrase</b>, et il ne fusionne pas avec la
  /// clause d'incomplétude : la première incomplétude se répare en repassant, la seconde ne se
  /// répare pas.
  /// </summary>
  [Fact]
  public void CountsTheUnfinishedArbitrationInOneSentence()
  {
    var screening = AScreening.Of(
      AScreening.AFlaggedColumn("adr_l1", position: 1),
      AScreening.AFlaggedColumn("dt_naiss", position: 2));

    PersonalDataMap.Of(screening, ExportedOn).UnfinishedArbitration
      .ShouldBe("2 colonnes de cette cartographie n'ont pas encore été arbitrées.");
  }

  /// <summary>Une seule colonne attend : le singulier, parce qu'on ne lit pas « 1 colonnes ».</summary>
  [Fact]
  public void AgreesTheSentenceWithItsCount()
  {
    var settled = AScreening.AFlaggedColumn("adr_l1", position: 1);
    var screening = AScreening.Of(settled, AScreening.AFlaggedColumn("dt_naiss", position: 2));

    screening.Arbitrate(settled.Identity, ScreenedColumnState.Retained, ExportedOn);

    PersonalDataMap.Of(screening, ExportedOn).UnfinishedArbitration
      .ShouldBe("1 colonne de cette cartographie n'a pas encore été arbitrée.");
  }

  /// <summary>
  /// ⚠️ <b>Absente, et non à zéro.</b> « 0 colonne attend encore » se lit comme une réserve alors
  /// qu'elle n'en est pas une.
  /// </summary>
  [Fact]
  public void SaysNothingWhenEveryColumnHasBeenRuledOn()
  {
    var first = AScreening.AFlaggedColumn("adr_l1", position: 1);
    var second = AScreening.AFlaggedColumn("dt_naiss", position: 2);
    var screening = AScreening.Of(first, second);

    screening.Arbitrate(first.Identity, ScreenedColumnState.Retained, ExportedOn);
    screening.Arbitrate(second.Identity, ScreenedColumnState.SetAside, ExportedOn);

    PersonalDataMap.Of(screening, ExportedOn).UnfinishedArbitration.ShouldBeNull();
  }

  /// <summary>
  /// Ce que le relevé porte en tête, une seule fois : la base, le dialecte, le moteur et les deux
  /// dates.
  /// </summary>
  [Fact]
  public void CarriesTheListingOnceAtTheHead()
  {
    var screening = AScreening.Of(AScreening.AFlaggedColumn());

    var map = PersonalDataMap.Of(screening, ExportedOn);

    map.Id.ShouldBe(screening.Id);
    map.Database.ShouldBe("galette_prod");
    map.Dialect.ShouldBe("postgresql");
    map.Engine.ShouldBe(AScreening.Engine);
    map.LaunchedOn.ShouldBe(AScreening.LaunchedOn);
    map.ExportedOn.ShouldBe(ExportedOn);
    map.Counts.Columns.ShouldBe(1);
  }

  /// <summary>
  /// ⚠️ <b>Un rapport chargé sans ses colonnes ne s'exporte pas.</b> Il porterait des comptes
  /// sincères et faux à côté d'une liste vide, dans un fichier qui promet de porter toutes les
  /// lignes. Le cas ne se construit pas ici — <c>Screening.Of</c> refuse déjà un relevé amputé —, il
  /// naît d'une lecture EF sans son <c>Include</c> : ce qui l'arrête est le garde de
  /// <see cref="ScreeningCounts.Of"/>, que la cartographie emprunte plutôt que de recompter.
  /// </summary>
  [Fact]
  public void LeansOnTheCountsRatherThanRecountingOnItsOwn()
  {
    var screening = AScreening.Of(
      AScreening.AFlaggedColumn("adr_l1", position: 1),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh", position: 2)));

    PersonalDataMap.Of(screening, ExportedOn).Counts.ShouldBe(ScreeningCounts.Of(screening));
  }

  [Fact]
  public void RefusesAnAbsentScreening()
  {
    Should.Throw<ArgumentNullException>(() => PersonalDataMap.Of(null!, ExportedOn));
  }
}
