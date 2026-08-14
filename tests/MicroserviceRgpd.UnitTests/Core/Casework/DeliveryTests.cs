using System.Text;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.SharedKernel;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas.
using Case = MicroserviceRgpd.Core.Casework.Case;

namespace MicroserviceRgpd.UnitTests.Core.Casework;

/// <summary>
/// Ce qu'une <c>Delivery</c> rassemble — et ce qu'elle refuse de joindre.
/// </summary>
/// <remarks>
/// <b>Ses pièces sont exactement celles que la première liste de la <c>DeliveryLetter</c> annonce.</b>
/// Joindre un fichier que la page range ailleurs ferait deux dires contradictoires dans le même
/// envoi, à la personne la moins bien placée pour les départager.
/// </remarks>
public class DeliveryTests
{
  private static readonly DateTimeOffset Opened = new(2026, 4, 10, 9, 0, 0, TimeSpan.Zero);
  private static readonly DeclaredSystemId Boutique = DeclaredSystemId.From("brocanto-boutique");
  private static readonly DeclaredSystemId Journal = DeclaredSystemId.From("brocanto-journal");

  /// <summary>Une par <c>Claim</c> : la remise porte un droit, et les pièces de ce droit seul.</summary>
  [Fact]
  public void GathersThePiecesOfOneRightAndOfNoOther()
  {
    var opened = ACase(DataSubjectRight.Access, DataSubjectRight.Portability);

    var delivery = Delivery.Of(
      opened,
      DataSubjectRight.Access,
      ALandscape(),
      [
        APiece(opened, Boutique, "nom;prénom"),
        APiece(opened, Journal, "des lignes", DataSubjectRight.Portability),
      ]);

    delivery.Right.ShouldBe(DataSubjectRight.Access);
    delivery.Case.ShouldBe(opened.Id);
    delivery.Pieces.Select(piece => piece.DeclaredSystem).ShouldBe([Boutique]);
  }

  /// <summary>
  /// <b>Une pièce vide n'est pas jointe.</b> Elle est une réponse datée, et la page de garde la dit
  /// en toutes lettres parmi les systèmes interrogés sans rattachement ; y ajouter un fichier de
  /// zéro octet ferait dire à l'envoi le contraire de ce que la page dit.
  /// </summary>
  [Fact]
  public void JoinsNoFileForASystemThatAnsweredWithNothing()
  {
    var opened = ACase();

    var delivery = Delivery.Of(
      opened,
      DataSubjectRight.Access,
      ALandscape(),
      [APiece(opened, Boutique, "nom;prénom"), APiece(opened, Journal, string.Empty)]);

    delivery.Pieces.Select(piece => piece.DeclaredSystem).ShouldBe([Boutique]);

    // Et la page, elle, ne perd pas ce système : elle le range dans sa deuxième liste.
    delivery.DeliveryLetter.Joined.Select(system => system.DeclaredSystem).ShouldBe([Boutique]);
    delivery.DeliveryLetter.QueriedWithoutAttachment
      .Select(system => system.DeclaredSystem)
      .ShouldContain(Journal);
  }

  /// <summary>
  /// <b>Les pièces jointes sont exactement la première liste de la page.</b> C'est la promesse que
  /// l'archive et la page ne peuvent pas se contredire.
  /// </summary>
  [Fact]
  public void JoinsExactlyWhatItsDeliveryLetterAnnounces()
  {
    var opened = ACase();

    var delivery = Delivery.Of(
      opened,
      DataSubjectRight.Access,
      ALandscape(),
      [APiece(opened, Journal, "des lignes"), APiece(opened, Boutique, "nom;prénom")]);

    delivery.Pieces.Select(piece => piece.DeclaredSystem)
      .ShouldBe(delivery.DeliveryLetter.Joined.Select(system => system.DeclaredSystem), ignoreOrder: true);
  }

  /// <summary>
  /// Le dénombrement que le <c>EvidenceLog</c> gardera porte sur <b>le même ensemble</b> que la page :
  /// deux ensembles mesurés l'un contre l'autre écriraient « 6 sur 5 ».
  /// </summary>
  [Fact]
  public void CountsTheSystemsItHadToAnswerForOnTheSameGroundAsThePage()
  {
    var opened = ACase();

    var sheet = Delivery.Of(
      opened,
      DataSubjectRight.Access,
      ALandscape(),
      [APiece(opened, Boutique, "nom;prénom")]).DeliveryLetter;

    sheet.RecordedSystemCount.ShouldBe(
      sheet.Joined.Count + sheet.QueriedWithoutAttachment.Count + sheet.NotCovered.Count);

    sheet.Joined.Count.ShouldBeLessThanOrEqualTo(sheet.RecordedSystemCount);
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
      ALandscape(),
      ReceptionDate.Declared(Opened));
  }

  private static Manifest ALandscape()
  {
    return Manifest.Of(
    [
      ASystem(Boutique, "La boutique", "Les commandes et les comptes clients de la boutique."),
      ASystem(Journal, "Le journal", "Les journaux applicatifs du serveur."),
    ]);
  }

  private static DeclaredSystem ASystem(DeclaredSystemId id, string label, string contents)
  {
    return DeclaredSystem.Declare(
      id,
      SystemLabel.From(label),
      SystemContents.From(contents),
      [Capability.Locate, Capability.Read],
      AdapterAddress.From("https://adapter.brocanto.example"),
      Opened.AddDays(-30));
  }

  private static RetrievedData APiece(
    Case opened,
    DeclaredSystemId system,
    string content,
    DataSubjectRight? right = null)
  {
    return RetrievedData.Of(
      opened.Id,
      right ?? DataSubjectRight.Access,
      system,
      new RetrievedPiece(
        TransportEnvelope.Of("text/csv", "export.csv", system),
        Encoding.UTF8.GetBytes(content)),
      Opened);
  }
}
