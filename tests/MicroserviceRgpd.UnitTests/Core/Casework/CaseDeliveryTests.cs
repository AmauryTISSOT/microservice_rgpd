using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas.
using Case = MicroserviceRgpd.Core.Casework.Case;

namespace MicroserviceRgpd.UnitTests.Core.Casework;

/// <summary>
/// Les <b>deux gestes</b> de la remise, tels que le dossier les retient.
/// </summary>
/// <remarks>
/// <b>Télécharger n'est pas remettre.</b> Le premier geste ouvre l'archive pour vérifier qu'elle
/// n'est pas vide ; le second seul affirme qu'on a rendu la réponse. Les confondre aurait daté la
/// preuve à l'instant où quelqu'un vérifiait, et détruit les pièces avant qu'il n'ait pu constater
/// que l'archive ne portait rien.
/// </remarks>
public class CaseDeliveryTests
{
  private static readonly DateTimeOffset Opened = new(2026, 4, 10, 9, 0, 0, TimeSpan.Zero);

  /// <summary>Un dossier neuf n'a ni remise prise, ni remise déclarée.</summary>
  [Fact]
  public void CarriesNoDeliveryUntilSomeoneTakesOne()
  {
    var claim = ACase().Claims.Single();

    claim.DeliveryTakenOn.ShouldBeNull();
    claim.DeliveryDeclaredOn.ShouldBeNull();
    claim.DeliveryAwaitsDeclaration.ShouldBeFalse();
  }

  /// <summary>
  /// Le premier geste date le téléchargement, et <b>rien d'autre</b> : la remise n'est pas datée.
  /// </summary>
  [Fact]
  public void DatesTheDownloadWithoutDatingTheHandover()
  {
    var opened = ACase();

    opened.TakeDelivery(DataSubjectRight.Access, Opened.AddDays(3)).ShouldBeTrue();

    var claim = opened.Claims.Single();

    claim.DeliveryTakenOn.ShouldBe(Opened.AddDays(3));
    claim.DeliveryDeclaredOn.ShouldBeNull();
  }

  /// <summary>
  /// <b>Une remise prise et jamais déclarée remonte dans le tableau des demandes RGPD.</b> C'est le
  /// seul propos de la date du premier geste : sans elle, le travail resté au milieu du gué serait
  /// invisible.
  /// </summary>
  [Fact]
  public void MakesADeliveryTakenAndNeverDeclaredVisible()
  {
    var opened = ACase();

    opened.AwaitsADeliveryDeclaration.ShouldBeFalse();

    opened.TakeDelivery(DataSubjectRight.Access, Opened.AddDays(3));
    opened.AwaitsADeliveryDeclaration.ShouldBeTrue();

    opened.DeclareDelivered(DataSubjectRight.Access, Opened.AddDays(4));
    opened.AwaitsADeliveryDeclaration.ShouldBeFalse();
  }

  /// <summary>
  /// Retélécharger n'est pas un fait nouveau : <b>le premier instant est gardé</b>, et il n'est
  /// jamais réécrit par un second passage.
  /// </summary>
  [Fact]
  public void KeepsTheFirstInstantRatherThanTheLast()
  {
    var opened = ACase();

    opened.TakeDelivery(DataSubjectRight.Access, Opened.AddDays(3));
    opened.TakeDelivery(DataSubjectRight.Access, Opened.AddDays(5));

    opened.Claims.Single().DeliveryTakenOn.ShouldBe(Opened.AddDays(3));
  }

  /// <summary>
  /// <b>Le second geste seul date la remise</b>, et il ne se déclare qu'une fois : une remise
  /// redéclarée le lendemain aurait fait glisser une date de preuve.
  /// </summary>
  [Fact]
  public void DatesTheHandoverOnceAndOnlyOnce()
  {
    var opened = ACase();

    opened.TakeDelivery(DataSubjectRight.Access, Opened.AddDays(3));

    opened.DeclareDelivered(DataSubjectRight.Access, Opened.AddDays(4)).ShouldBeTrue();
    opened.DeclareDelivered(DataSubjectRight.Access, Opened.AddDays(9)).ShouldBeFalse();

    opened.Claims.Single().DeliveryDeclaredOn.ShouldBe(Opened.AddDays(4));
  }

  /// <summary>
  /// <b>Le second geste demande le premier.</b> Déclarer remis un fichier que personne n'a jamais
  /// eu en main daterait un geste qui n'a pas eu lieu.
  /// </summary>
  [Fact]
  public void RefusesToDateAHandoverOfSomethingNoOneEverHeld()
  {
    var opened = ACase();

    opened.DeclareDelivered(DataSubjectRight.Access, Opened.AddDays(4)).ShouldBeFalse();
    opened.Claims.Single().DeliveryDeclaredOn.ShouldBeNull();
  }

  /// <summary>
  /// <b>Une remise déclarée ne se retélécharge pas.</b> Ses pièces sont détruites : l'archive ne
  /// porterait plus que la page de garde.
  /// </summary>
  [Fact]
  public void RefusesToHandBackWhatItHasAlreadyDestroyed()
  {
    var opened = ACase();

    opened.TakeDelivery(DataSubjectRight.Access, Opened.AddDays(3));
    opened.DeclareDelivered(DataSubjectRight.Access, Opened.AddDays(4));

    opened.TakeDelivery(DataSubjectRight.Access, Opened.AddDays(5)).ShouldBeFalse();
  }

  /// <summary>
  /// <b>Une remise par <c>Claim</c>, jamais une par <c>Case</c>.</b> Deux droits sont deux réponses
  /// et deux dates de remise : remettre l'un ne remet pas l'autre.
  /// </summary>
  [Fact]
  public void DeliversOneRightWithoutDeliveringTheOther()
  {
    var opened = ACase(DataSubjectRight.Access, DataSubjectRight.Portability);

    opened.TakeDelivery(DataSubjectRight.Access, Opened.AddDays(3));
    opened.DeclareDelivered(DataSubjectRight.Access, Opened.AddDays(4));

    var portability = opened.Claims.Single(claim => claim.Right == DataSubjectRight.Portability);

    portability.DeliveryTakenOn.ShouldBeNull();
    portability.DeliveryDeclaredOn.ShouldBeNull();
  }

  /// <summary>Un droit que le dossier ne porte pas ne se remet pas — sans que rien ne casse.</summary>
  [Fact]
  public void RefusesTheDeliveryOfARightTheCaseDoesNotCarry()
  {
    var opened = ACase();

    opened.TakeDelivery(DataSubjectRight.Portability, Opened.AddDays(3)).ShouldBeFalse();
    opened.DeclareDelivered(DataSubjectRight.Portability, Opened.AddDays(4)).ShouldBeFalse();
  }

  /// <summary>Les instants entrent en UTC : deux remises datées du même moment se comparent.</summary>
  [Fact]
  public void NormalisesBothGesturesOntoUtc()
  {
    var opened = ACase();
    var local = new DateTimeOffset(2026, 4, 13, 11, 0, 0, TimeSpan.FromHours(2));

    opened.TakeDelivery(DataSubjectRight.Access, local);
    opened.DeclareDelivered(DataSubjectRight.Access, local.AddHours(1));

    var claim = opened.Claims.Single();

    claim.DeliveryTakenOn!.Value.Offset.ShouldBe(TimeSpan.Zero);
    claim.DeliveryTakenOn.ShouldBe(new DateTimeOffset(2026, 4, 13, 9, 0, 0, TimeSpan.Zero));
    claim.DeliveryDeclaredOn!.Value.Offset.ShouldBe(TimeSpan.Zero);
  }

  /// <summary>Un droit absent est une programmation fautive, pas un cas de bord à absorber.</summary>
  [Fact]
  public void RefusesAGestureThatNamesNoRight()
  {
    var opened = ACase();

    Should.Throw<ArgumentNullException>(() => opened.TakeDelivery(null!, Opened));
    Should.Throw<ArgumentNullException>(() => opened.DeclareDelivered(null!, Opened));
  }

  private static Case ACase(params DataSubjectRight[] rights)
  {
    return Case.Open(
      CaseId.Next(),
      IdentityDeclaration.Unverified,
      motivation: null,
      [Designation.Of(DesignationKind.Email, "jean.dupont@example.fr")],
      rights.Length == 0 ? [DataSubjectRight.Access] : rights,
      ClaimOrigin.Named,
      Manifest.Empty,
      ReceptionDate.Declared(Opened));
  }
}
