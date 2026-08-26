using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// L'aperçu est <b>soit</b> des valeurs, <b>soit</b> une raison — jamais rien, jamais les deux. La
/// troisième forme est celle qui rétablit l'<c>Omission silencieuse</c> par une cellule vide : « on
/// n'a pas regardé cette colonne » et « cette colonne ne contenait rien » se liraient pareil.
/// </summary>
public class ColumnPreviewTests
{
  [Fact]
  public void CarriesTheValuesItWasReadInTheOrderTheyCame()
  {
    var preview = ColumnPreview.Read(
    [
      PreviewedValue.Of("marie.dupont@example.com", actualLength: 24),
      PreviewedValue.Of("j.martin@example.com", actualLength: 20),
    ]);

    preview.CarriesValues.ShouldBeTrue();
    preview.Absence.ShouldBeNull();
    preview.Values.Select(value => value.Display).ShouldBe(
      ["marie.dupont@example.com", "j.martin@example.com"]);
  }

  [Fact]
  public void CarriesAReasonWhenThereIsNothingToShow()
  {
    var preview = ColumnPreview.Absent(PreviewAbsenceReason.AccessDenied);

    preview.CarriesValues.ShouldBeFalse();
    preview.Absence.ShouldBe(PreviewAbsenceReason.AccessDenied);
    preview.Values.ShouldBeEmpty();
  }

  /// <summary>
  /// ⚠️ <b>Un aperçu n'est jamais vide.</b> Une absence se dit par une raison nommée, jamais par le
  /// silence.
  /// </summary>
  [Fact]
  public void RefusesAnEmptyPreview()
  {
    var refusal = Should.Throw<ArgumentException>(() => ColumnPreview.Read([]));

    refusal.Message.ShouldContain("Absent");
  }

  /// <summary>
  /// ⚠️ <b>Jamais des valeurs ET une raison.</b> Le type ne l'interdit pas par un garde : il n'offre
  /// aucun chemin qui les pose ensemble — deux fabriques, et rien d'autre.
  /// </summary>
  [Fact]
  public void OffersNoWayToCarryBothValuesAndAReason()
  {
    typeof(ColumnPreview).GetConstructors().ShouldBeEmpty();

    ColumnPreview.Read([PreviewedValue.NullValue]).Absence.ShouldBeNull();
    ColumnPreview.Absent(PreviewAbsenceReason.ReadFailed).Values.ShouldBeEmpty();
  }

  [Fact]
  public void RefusesAnAbsenceThatNamesNoReason()
  {
    Should.Throw<ArgumentNullException>(() => ColumnPreview.Absent(null!));
  }

  /// <summary>
  /// ⚠️ <b>La borne de prélèvement vit ici, et c'est le type qui la tient.</b> Au-delà, ce n'est plus
  /// un aperçu qu'un humain lit de ses yeux : c'est de l'analyse de contenu, et la frontière de
  /// l'<c>ADR-0012</c> est franchie.
  /// </summary>
  [Fact]
  public void RefusesMoreValuesThanTheSamplingBoundAllows()
  {
    var six = Enumerable
      .Range(1, ColumnPreview.MaxValues + 1)
      .Select(index => PreviewedValue.Of($"valeur {index}", actualLength: 9))
      .ToList();

    Should.Throw<ArgumentException>(() => ColumnPreview.Read(six));

    Should.NotThrow(() => ColumnPreview.Read(six.Take(ColumnPreview.MaxValues)));
  }

  /// <summary>
  /// ⚠️ <b>Les deux bornes sont lisibles du dehors, et elles n'ont pas d'autre source.</b> La requête
  /// de prélèvement lit celle-ci ; la clause qui écrit « cinq valeurs au plus » la lit au même
  /// endroit. Écrit à la main des deux côtés, le chiffre divergerait au premier changement de borne
  /// — et c'est le texte rendu à l'<c>Operator</c> qui se mettrait à mentir.
  /// </summary>
  [Fact]
  public void PublishesTheTwoBoundsAsTheirOnlySource()
  {
    ColumnPreview.MaxValues.ShouldBe(5);

    // 254 : la plus longue valeur à motif utile — 34 pour un IBAN, 254 pour un courriel, RFC 5321.
    ColumnPreview.MaxValueLength.ShouldBe(254);
  }
}

/// <summary>
/// Une valeur lue : le texte coupé par le SGBD, et la longueur réelle relevée à côté. ⚠️ <c>NULL</c>
/// et la chaîne vide en sont deux, distinctes, <b>et ce ne sont pas des absences</b>.
/// </summary>
public class PreviewedValueTests
{
  /// <summary>
  /// <b>Cinq <c>NULL</c> sont cinq valeurs lues</b> : le prélèvement a parfaitement réussi et aucune
  /// raison n'est due. Rendus en cellules blanches, ils se liraient comme l'absence que la raison
  /// nommée vient de chasser, un cran plus bas.
  /// </summary>
  [Fact]
  public void ReadsNullAndTheEmptyStringAsTwoDistinctValues()
  {
    PreviewedValue.NullValue.Display.ShouldBe("⟨null⟩");
    PreviewedValue.EmptyText.Display.ShouldBe("⟨vide⟩");

    PreviewedValue.NullValue.Display.ShouldNotBe(PreviewedValue.EmptyText.Display);

    PreviewedValue.NullValue.IsNull.ShouldBeTrue();
    PreviewedValue.NullValue.IsEmpty.ShouldBeFalse();

    PreviewedValue.EmptyText.IsNull.ShouldBeFalse();
    PreviewedValue.EmptyText.IsEmpty.ShouldBeTrue();
  }

  /// <summary>
  /// <b>Un aperçu de cinq <c>NULL</c> est un aperçu de valeurs</b>, et non une absence déguisée : il
  /// ne porte aucune raison.
  /// </summary>
  [Fact]
  public void MakesAPreviewOfNullsAPreviewOfValues()
  {
    var preview = ColumnPreview.Read(Enumerable.Repeat(PreviewedValue.NullValue, 5));

    preview.CarriesValues.ShouldBeTrue();
    preview.Absence.ShouldBeNull();
  }

  /// <summary>
  /// ⚠️ <b>La longueur réelle accompagne toute valeur</b>, et sans elle la clause qui écarte du
  /// compte une valeur tronquée serait écrite sans jamais pouvoir s'appliquer.
  /// </summary>
  [Fact]
  public void KeepsTheRealLengthBesideTheCutText()
  {
    var cut = PreviewedValue.Of(new string('a', ColumnPreview.MaxValueLength), actualLength: 4_096);

    cut.ActualLength.ShouldBe(4_096);
    cut.IsTruncated.ShouldBeTrue();

    var whole = PreviewedValue.Of("0612345678", actualLength: 10);

    whole.IsTruncated.ShouldBeFalse();
  }

  [Fact]
  public void ReadsNullAndTheEmptyStringAsUntruncatedValuesOfNoLength()
  {
    PreviewedValue.NullValue.ActualLength.ShouldBe(0);
    PreviewedValue.NullValue.IsTruncated.ShouldBeFalse();

    PreviewedValue.EmptyText.ActualLength.ShouldBe(0);
    PreviewedValue.EmptyText.IsTruncated.ShouldBeFalse();
  }

  /// <summary>
  /// Les deux valeurs à marqueur ont leur fabrique nommée, et le texte ne les fabrique pas : un
  /// <c>null</c> ou une chaîne vide passés à <see cref="PreviewedValue.Of"/> seraient une valeur à
  /// laquelle l'écran n'aurait pas de marqueur à donner.
  /// </summary>
  [Fact]
  public void SendsNullAndTheEmptyStringToTheirOwnFactories()
  {
    Should.Throw<ArgumentNullException>(() => PreviewedValue.Of(null!, actualLength: 0));

    var refusal = Should.Throw<ArgumentException>(
      () => PreviewedValue.Of(string.Empty, actualLength: 0));

    refusal.Message.ShouldContain(nameof(PreviewedValue.EmptyText));
  }

  /// <summary>
  /// La coupure est faite <b>par le SGBD</b>, avant la traversée du réseau : une valeur plus longue
  /// arrivée jusqu'ici est une requête de prélèvement qui a cessé de couper.
  /// </summary>
  [Fact]
  public void RefusesATextLongerThanTheCuttingBound()
  {
    Should.Throw<ArgumentException>(() => PreviewedValue.Of(
      new string('a', ColumnPreview.MaxValueLength + 1),
      actualLength: 10_000));
  }

  /// <summary>Une coupure ne rallonge pas : ce couple-là rendrait « tronquée » faux dans les deux sens.</summary>
  [Fact]
  public void RefusesARealLengthShorterThanTheTextItAccompanies()
  {
    Should.Throw<ArgumentOutOfRangeException>(
      () => PreviewedValue.Of("0612345678", actualLength: 3));
  }
}
