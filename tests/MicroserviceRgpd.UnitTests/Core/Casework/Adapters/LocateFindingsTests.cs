using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;

namespace MicroserviceRgpd.UnitTests.Core.Casework.Adapters;

/// <summary>
/// Ce que le service consent à tenir pour vrai d'un corps de <c>locate</c> — et ce qu'il refuse de
/// compléter.
/// </summary>
/// <remarks>
/// Deux faits cardinaux : <b>un zéro est un zéro unique</b>, sans variante à distinguer ; et
/// <b>rien n'est écarté en silence</b> — une ligne qu'on ne sait pas lire est une panne, jamais une
/// ligne de moins, parce qu'elle nomme les données de quelqu'un.
/// </remarks>
public class LocateFindingsTests
{
  private static readonly DeclaredSystemId Boutique = DeclaredSystemId.From("brocanto-boutique");

  /// <summary>
  /// Le noyau certain et les réserves, relus tels que l'application les a écrits — la <b>prose de
  /// motif comprise</b>, qui est ce que l'<c>Operator</c> lira pour arbitrer.
  /// </summary>
  [Fact]
  public void ReadsTheCertainCoreAndTheReasonedReserves()
  {
    var findings = LocateFindings.ReadFrom(
      new LocateOnTheWire(
        ["clients#1203"],
        [
          new ReservedOnTheWire(
            "clients#4417",
            "Deux comptes portent ce nom : celui-ci a été créé en 2019 et n'a jamais commandé.",
            [new DesignationOnTheWire("email", "Jean.Dupont@Example.fr")]),
        ]),
      Boutique);

    findings.Certain.ShouldHaveSingleItem().Value.ShouldBe("clients#1203");

    var reserved = findings.Reserved.ShouldHaveSingleItem();

    reserved.Reference.Value.ShouldBe("clients#4417");
    reserved.Reason.ShouldBe("Deux comptes portent ce nom : celui-ci a été créé en 2019 et n'a jamais commandé.");
    reserved.State.ShouldBe(ReservationState.Awaiting);
    reserved.Designations.ShouldHaveSingleItem().Value.ShouldBe("Jean.Dupont@Example.fr");
  }

  /// <summary>
  /// <b>Un <c>Locate</c> sans rattachement est un zéro unique.</b> Un corps vide, des listes vides,
  /// des listes absentes : trois écritures d'une seule et même réponse. L'<c>Adapter</c> n'a pas à
  /// distinguer « cherché, rien trouvé » de « désignation insuffisante » — distinction qu'il ne peut
  /// pas faire.
  /// </summary>
  [Fact]
  public void ReadsEveryShapeOfNothingAsTheSameSingleZero()
  {
    LocateFindings.ReadFrom(served: null, Boutique).IsNothing.ShouldBeTrue();
    LocateFindings.ReadFrom(new LocateOnTheWire(null, null), Boutique).IsNothing.ShouldBeTrue();
    LocateFindings.ReadFrom(new LocateOnTheWire([], []), Boutique).IsNothing.ShouldBeTrue();
  }

  /// <summary>
  /// <b>Une réserve sans motif est une panne, jamais une réserve muette.</b> Sans prose, l'écran
  /// redeviendrait une case à cocher et l'<c>Operator</c> arbitrerait sans rien savoir ; l'écarter en
  /// silence serait pire encore — l'<c>Omission silencieuse</c> fabriquée par le service lui-même.
  /// </summary>
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void CallsAReserveWithoutAReasonABreakdown(string? reason)
  {
    Should.Throw<AdapterFailure>(() => LocateFindings.ReadFrom(
      new LocateOnTheWire(null, [new ReservedOnTheWire("clients#4417", reason, null)]),
      Boutique));
  }

  /// <summary>
  /// Une référence vide ne désigne rien, et une réserve vide non plus. Le service ne les complète pas
  /// et ne les laisse pas tomber : il les rapporte en panne, nommément.
  /// </summary>
  [Fact]
  public void CallsAnUnreadableLineABreakdown()
  {
    Should.Throw<AdapterFailure>(() => LocateFindings.ReadFrom(
      new LocateOnTheWire([" "], null),
      Boutique));

    Should.Throw<AdapterFailure>(() => LocateFindings.ReadFrom(
      new LocateOnTheWire(null, [null]),
      Boutique));
  }

  /// <summary>
  /// <b>Le vocabulaire des désignations est fermé des deux côtés du fil.</b> Un cinquième mot n'est
  /// pas une nature qu'on devine, et la ranger « au mieux » ferait chercher la personne sous une clé
  /// inventée par le service.
  /// </summary>
  [Fact]
  public void RefusesADesignationOfAKindTheContractDoesNotKnow()
  {
    var failure = Should.Throw<AdapterFailure>(() => LocateFindings.ReadFrom(
      new LocateOnTheWire(
        null,
        [new ReservedOnTheWire("clients#4417", "Un doute.", [new DesignationOnTheWire("iban", "FR76…")])]),
      Boutique));

    // Le message nomme le système : c'est chez ce client-là qu'on va réparer.
    failure.Message.ShouldContain("brocanto-boutique");
  }

  /// <summary>
  /// Deux fois la même référence, ce serait deux fois la même ligne : la seconde n'apprend rien et
  /// ferait arbitrer deux fois le même doute.
  /// </summary>
  [Fact]
  public void KeepsTheSameLineOnlyOnce()
  {
    var findings = LocateFindings.ReadFrom(
      new LocateOnTheWire(
        ["clients#1203", "clients#1203"],
        [
          new ReservedOnTheWire("clients#4417", "Un doute.", null),
          new ReservedOnTheWire("clients#4417", "Le même doute, redit.", null),
        ]),
      Boutique);

    findings.Certain.Count.ShouldBe(1);
    findings.Reserved.ShouldHaveSingleItem().Reason.ShouldBe("Un doute.");
  }

  /// <summary>
  /// <b>Une réserve sans désignation reste locale et opaque</b> : le service n'apprendra jamais ce que
  /// « #1203 » désignait, quoi qu'un humain en arbitre.
  /// </summary>
  [Fact]
  public void LeavesAReserveWithoutDesignationsPurelyLocal()
  {
    var findings = LocateFindings.ReadFrom(
      new LocateOnTheWire(null, [new ReservedOnTheWire("#1203", "Une ligne d'une table héritée.", null)]),
      Boutique);

    findings.Reserved.ShouldHaveSingleItem().Designations.ShouldBeEmpty();
    findings.IsNothing.ShouldBeFalse();
  }
}
