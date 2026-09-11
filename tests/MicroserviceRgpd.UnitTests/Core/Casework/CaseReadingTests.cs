using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.SharedKernel;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas.
using Case = MicroserviceRgpd.Core.Casework.Case;

namespace MicroserviceRgpd.UnitTests.Core.Casework;

/// <summary>
/// Ce qu'un <c>Read</c> laisse dans un dossier — et surtout ce qu'il n'y laisse pas.
/// </summary>
/// <remarks>
/// Deux faits cardinaux : <b>le dossier ne porte aucun octet</b> — la pièce vit hors de l'agrégat,
/// pour que la remise l'efface sans le réécrire ; et <b>le grain est le couple (droit, système)</b>,
/// à la différence du <c>Locate</c>, dont le grain est le système seul.
/// </remarks>
public class CaseReadingTests
{
  private static readonly DateTimeOffset Opened = new(2026, 4, 10, 9, 0, 0, TimeSpan.Zero);
  private static readonly DeclaredSystemId Boutique = DeclaredSystemId.From("brocanto-boutique");
  private static readonly DeclaredSystemId Journal = DeclaredSystemId.From("brocanto-journal");

  /// <summary>
  /// <b>Le <c>null</c> n'est pas une pièce vide.</b> « Pas appelé » et « appelé, rien rendu » sont
  /// deux déclarations différentes, et seule la seconde a une valeur de preuve.
  /// </summary>
  [Fact]
  public void SaysNothingOfASystemItNeverRead()
  {
    ACase().ReadingIn(DataSubjectRight.Access, Boutique).ShouldBeNull();
  }

  /// <summary>
  /// Une lecture servie retient <b>le fait daté et le compte du sac</b> — jamais un octet, jamais un
  /// nom de fichier, jamais une taille.
  /// </summary>
  [Fact]
  public void KeepsTheDatedFactAndTheCountOfTheBag()
  {
    var opened = ACase();

    opened.ReadServed(DataSubjectRight.Access, Boutique, Opened.AddHours(2));

    var reading = opened.ReadingIn(DataSubjectRight.Access, Boutique).ShouldNotBeNull();

    reading.LastOutcome.ShouldBe(AdapterOutcome.Served);
    reading.Right.ShouldBe(DataSubjectRight.Access);
    reading.DeclaredSystem.ShouldBe(Boutique);
    reading.AskedAt.ShouldBe(Opened.AddHours(2));
    reading.DesignationsAtCall.ShouldBe(1);
    reading.DeclaredDeadline.ShouldBeNull();
  }

  /// <summary>
  /// <b>Le grain est le couple (droit, système).</b> Lire au titre de l'art. 15 et au titre de
  /// l'art. 20 sont deux lectures : le périmètre matériel n'est pas le même, et il se décide chez le
  /// client, ligne par ligne.
  /// </summary>
  [Fact]
  public void GrainsARightAndASystemSeparately()
  {
    var opened = ACase(DataSubjectRight.Access, DataSubjectRight.Portability);

    opened.ReadServed(DataSubjectRight.Access, Boutique, Opened);
    opened.ReadServed(DataSubjectRight.Portability, Boutique, Opened);
    opened.ReadServed(DataSubjectRight.Access, Journal, Opened);

    opened.Readings.Count.ShouldBe(3);

    opened.ReadingIn(DataSubjectRight.Portability, Journal).ShouldBeNull();
  }

  /// <summary>
  /// Repasser sur le <b>même</b> couple ne crée pas une seconde lecture : il en réécrit une, qui ne
  /// dit jamais que le dernier appel.
  /// </summary>
  [Fact]
  public void RewritesTheOneReadingRatherThanPilingThemUp()
  {
    var opened = ACase();

    opened.ReadDeferred(DataSubjectRight.Access, Boutique, Opened.AddDays(2), Opened);
    opened.ReadServed(DataSubjectRight.Access, Boutique, Opened.AddDays(3));

    var reading = opened.Readings.ShouldHaveSingleItem();

    reading.LastOutcome.ShouldBe(AdapterOutcome.Served);
    reading.AskedAt.ShouldBe(Opened.AddDays(3));
  }

  /// <summary>
  /// <b>Un <c>202</c> porte son échéance déclarée</b>, et c'est elle qui fera repasser — à l'ouverture
  /// du dossier, jamais par une minuterie.
  /// </summary>
  [Fact]
  public void CarriesTheDeadlineADeferralDeclared()
  {
    var opened = ACase();

    opened.ReadDeferred(DataSubjectRight.Access, Boutique, Opened.AddDays(2), Opened);

    var reading = opened.ReadingIn(DataSubjectRight.Access, Boutique).ShouldNotBeNull();

    reading.LastOutcome.ShouldBe(AdapterOutcome.Deferred);
    reading.DeclaredDeadline.ShouldBe(Opened.AddDays(2));
  }

  /// <summary>Servir après avoir différé <b>efface l'échéance</b> : elle n'attend plus rien.</summary>
  [Fact]
  public void ForgetsTheDeadlineOnceTheAdapterFinallyServed()
  {
    var opened = ACase();

    opened.ReadDeferred(DataSubjectRight.Access, Boutique, Opened.AddDays(2), Opened);
    opened.ReadServed(DataSubjectRight.Access, Boutique, Opened.AddDays(3));

    opened.ReadingIn(DataSubjectRight.Access, Boutique)!.DeclaredDeadline.ShouldBeNull();
  }

  /// <summary>
  /// <b>Les deux refus se gardent distincts.</b> Un secret refusé se répare dans la configuration de
  /// déploiement ; un système non servi se répare dans le <c>Manifest</c> ou chez le client. Les
  /// confondre ferait chercher au mauvais endroit.
  /// </summary>
  [Theory]
  [InlineData(nameof(AdapterOutcome.SecretRefused))]
  [InlineData(nameof(AdapterOutcome.SystemNotServed))]
  public void TellsTheTwoRefusalsApart(string refusal)
  {
    var opened = ACase();

    opened.ReadRefused(DataSubjectRight.Access, Boutique, AdapterOutcome.FromName(refusal), Opened);

    opened.ReadingIn(DataSubjectRight.Access, Boutique)!
      .LastOutcome.ShouldBe(AdapterOutcome.FromName(refusal));
  }

  /// <summary>
  /// <b>Ce qui n'est pas un refus ne s'inscrit pas comme tel.</b> Un « servi » rangé sous un refus
  /// ferait dire au dossier une chose que personne n'a répondue.
  /// </summary>
  [Theory]
  [InlineData(nameof(AdapterOutcome.Served))]
  [InlineData(nameof(AdapterOutcome.Deferred))]
  public void RefusesToRecordAsARefusalWhatIsNotOne(string outcome)
  {
    var opened = ACase();

    Should.Throw<ArgumentException>(() =>
      opened.ReadRefused(DataSubjectRight.Access, Boutique, AdapterOutcome.FromName(outcome), Opened));
  }

  /// <summary>
  /// Le compte du sac est celui du <b>jour de l'appel</b> : c'est lui qui dira qu'une lecture menée
  /// sous moins de désignations n'avait pas la même portée, et qu'il faut repasser.
  /// </summary>
  [Fact]
  public void RemembersHowManyDesignationsTheCallWentUnder()
  {
    var opened = ACase();

    opened.ReadServed(DataSubjectRight.Access, Boutique, Opened);
    opened.ReadingIn(DataSubjectRight.Access, Boutique)!.DesignationsAtCall.ShouldBe(1);

    opened.LocateServed(
      Boutique,
      LocateFindings.ReadFrom(
        new LocateOnTheWire(
          null,
          [new ReservedOnTheWire(
            "clients#4417",
            "Deux comptes portent ce nom.",
            [new DesignationOnTheWire("email", "Jean.Dupont@Example.fr")])]),
        Boutique),
      Opened);

    opened.Arbitrate(Boutique, OpaqueReference.Of("clients#4417"), ReservationState.Attached);

    opened.ReadServed(DataSubjectRight.Access, Boutique, Opened.AddDays(1));
    opened.ReadingIn(DataSubjectRight.Access, Boutique)!.DesignationsAtCall.ShouldBe(2);
  }

  /// <summary>Les instants entrent en UTC : deux lectures datées du même moment se comparent.</summary>
  [Fact]
  public void NormalisesEveryInstantOntoUtc()
  {
    var opened = ACase();
    var local = new DateTimeOffset(2026, 4, 10, 11, 0, 0, TimeSpan.FromHours(2));

    opened.ReadDeferred(DataSubjectRight.Access, Boutique, local.AddDays(1), local);

    var reading = opened.ReadingIn(DataSubjectRight.Access, Boutique)!;

    reading.AskedAt.Offset.ShouldBe(TimeSpan.Zero);
    reading.DeclaredDeadline!.Value.Offset.ShouldBe(TimeSpan.Zero);
    reading.AskedAt.ShouldBe(Opened);
  }

  /// <summary>Un droit absent est une programmation fautive, pas un cas de bord à absorber.</summary>
  [Fact]
  public void RefusesAReadingThatNamesNoRight()
  {
    var opened = ACase();

    Should.Throw<ArgumentNullException>(() => opened.ReadingIn(null!, Boutique));
    Should.Throw<ArgumentNullException>(() => opened.ReadServed(null!, Boutique, Opened));
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
      [],
      ReceptionDate.Declared(Opened));
  }
}
