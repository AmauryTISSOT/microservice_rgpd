using System.Text;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.SharedKernel;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas.
using Case = MicroserviceRgpd.Core.Casework.Case;

namespace MicroserviceRgpd.UnitTests.Core.Casework;

/// <summary>
/// La composition de la <c>DeliveryLetter</c> à partir du <c>Manifest</c> et des <c>Step</c> — et ce
/// qu'elle refuse d'écrire.
/// </summary>
/// <remarks>
/// Le test le plus important de ce fichier n'est aucun des trois rangements : c'est
/// <see cref="WritesNotASingleFigure"/>. « Énumérer, jamais compter » est ce qui sépare une page
/// actionnable — « l'export commercial transmis chaque mois à notre agence » — d'un « 4 sur 6 » qui
/// n'apprend rien à la personne et lui ment sur l'exhaustivité du recensement.
/// </remarks>
public class DeliveryLetterTests
{
  private static readonly DateTimeOffset Opened = new(2026, 4, 10, 9, 0, 0, TimeSpan.Zero);
  private static readonly DeclaredSystemId Boutique = DeclaredSystemId.From("brocanto-boutique");
  private static readonly DeclaredSystemId Journal = DeclaredSystemId.From("brocanto-journal");
  private static readonly DeclaredSystemId Agence = DeclaredSystemId.From("brocanto-agence");

  private const string AgenceContents = "L'export commercial transmis chaque mois à notre agence.";

  /// <summary>
  /// <b>Une pièce pleine, et elle seule, vaut « joint ».</b> C'est la seule des trois listes qui
  /// promette un contenu à la personne.
  /// </summary>
  [Fact]
  public void JoinsTheSystemsThatServedAFullPiece()
  {
    var opened = ACase();

    var sheet = DeliveryLetter.Compose(
      opened,
      DataSubjectRight.Access,
      ALandscape(),
      [APieceFrom(opened, Boutique, "nom;prénom\nDupont;Jean")]);

    sheet.Joined.Select(system => system.DeclaredSystem).ShouldBe([Boutique]);
  }

  /// <summary>
  /// <b>Une pièce vide n'est pas une réponse</b>, et la fondre dans la première liste ferait
  /// promettre un contenu qui n'existe pas. Elle dit « interrogé, rien », ce qui est la deuxième.
  /// </summary>
  [Fact]
  public void RefusesToJoinAnEmptyPiece()
  {
    var opened = ACase();

    var sheet = DeliveryLetter.Compose(
      opened,
      DataSubjectRight.Access,
      ALandscape(),
      [APieceFrom(opened, Boutique, string.Empty)]);

    sheet.Joined.ShouldBeEmpty();
    sheet.QueriedWithoutAttachment.Select(system => system.DeclaredSystem).ShouldBe([Boutique]);
  }

  /// <summary>
  /// <b>Un système interrogé qui n'a rien rattaché entre dans la deuxième liste</b> — celle qui porte
  /// le doute, et jamais « vous n'avez rien chez nous ».
  /// </summary>
  [Fact]
  public void SaysWhichSystemsWereQueriedWithoutFindingAnything()
  {
    var opened = ACase();

    opened.LocateServed(Journal, NothingFoundIn(Journal), Opened);

    var sheet = DeliveryLetter.Compose(opened, DataSubjectRight.Access, ALandscape(), []);

    sheet.QueriedWithoutAttachment.Select(system => system.DeclaredSystem).ShouldBe([Journal]);
  }

  /// <summary>
  /// ⚠️ <b>Une réserve en attente n'est pas un zéro.</b> Quelque chose a été trouvé sous ce qu'on
  /// avait, et ce qui manque est un regard : écrire « interrogé sans rattachement » juste au-dessus
  /// des lignes que le système vient de rendre serait faux.
  /// </summary>
  [Fact]
  public void NeverCallsAnAwaitingReservationAZero()
  {
    var opened = ACase();

    opened.LocateServed(Journal, AReservationIn(Journal), Opened);

    var sheet = DeliveryLetter.Compose(opened, DataSubjectRight.Access, ALandscape(), []);

    sheet.QueriedWithoutAttachment.ShouldBeEmpty();
    sheet.NotCovered.Select(system => system.DeclaredSystem).ShouldContain(Journal);
  }

  /// <summary>
  /// <b>Un système jamais appelé n'est pas un système qui n'a rien rendu.</b> Il n'est pas couvert, et
  /// c'est ce que la personne doit lire pour savoir quoi réclamer.
  /// </summary>
  [Fact]
  public void NamesTheUncoveredSystemsOneByOneInTheWordsOfTheContentsField()
  {
    var opened = ACase();

    var sheet = DeliveryLetter.Compose(opened, DataSubjectRight.Access, ALandscape(), []);

    var agence = sheet.NotCovered.Single(system => system.DeclaredSystem == Agence);

    agence.Contents.ShouldNotBeNull();
    agence.Contents.Value.Value.ShouldBe(AgenceContents);
    sheet.Write().ShouldContain(AgenceContents);
  }

  /// <summary>
  /// <b>Les trois listes forment une partition</b> des systèmes dont ce droit portait le travail dû :
  /// aucun système n'est perdu, aucun n'est compté deux fois. Une ligne qui s'évaporerait serait
  /// l'<c>Omission silencieuse</c> écrite de la main du service.
  /// </summary>
  [Fact]
  public void LosesNoSystemAndCountsNoneTwice()
  {
    var opened = ACase();

    opened.LocateServed(Journal, NothingFoundIn(Journal), Opened);

    var sheet = DeliveryLetter.Compose(
      opened,
      DataSubjectRight.Access,
      ALandscape(),
      [APieceFrom(opened, Boutique, "nom;prénom")]);

    var ranged = sheet.Joined
      .Concat(sheet.QueriedWithoutAttachment)
      .Concat(sheet.NotCovered)
      .Select(system => system.DeclaredSystem)
      .ToArray();

    ranged.ShouldBe([Boutique, Journal, Agence], ignoreOrder: true);
    ranged.Distinct().Count().ShouldBe(ranged.Length);
  }

  /// <summary>
  /// <b>Une pièce détenue est toujours annoncée</b>, même si son système n'a jamais porté de travail
  /// dû sur ce dossier — un système déclaré <em>après</em> l'ouverture, par exemple. La page de
  /// garde annoncerait sinon moins que ce que la personne reçoit, ce qui est la façon la plus sûre
  /// de lui faire croire qu'elle a tout.
  /// </summary>
  [Fact]
  public void AnnouncesAPieceWhoseSystemCarriesNoWorkOnThisCase()
  {
    var opened = ACase();
    var late = DeclaredSystemId.From("brocanto-tardif");

    var sheet = DeliveryLetter.Compose(
      opened,
      DataSubjectRight.Access,
      [
        .. ALandscape(),
        ASystem(late, "Le tardif", "Ce qu'on a déclaré après coup.", reachable: true),
      ],
      [APieceFrom(opened, late, "nom;prénom")]);

    sheet.Joined.Select(system => system.DeclaredSystem).ShouldContain(late);
    sheet.Write().ShouldContain("Le tardif");
  }

  /// <summary>
  /// <b>Le grain est le droit.</b> La pièce rapportée au titre d'un autre droit n'entre pas dans
  /// cette page : deux droits sont deux réponses et deux dates de remise.
  /// </summary>
  [Fact]
  public void IgnoresThePiecesOfAnotherRight()
  {
    var opened = ACase(DataSubjectRight.Access, DataSubjectRight.Portability);

    var sheet = DeliveryLetter.Compose(
      opened,
      DataSubjectRight.Access,
      ALandscape(),
      [APieceFrom(opened, Boutique, "nom;prénom", DataSubjectRight.Portability)]);

    sheet.Joined.ShouldBeEmpty();
  }

  /// <summary>
  /// <b>Un système que le catalogue ne porte plus garde sa ligne</b>, et la page le dit. Une ligne qui
  /// disparaîtrait parce que quelqu'un a révisé le recensement serait l'<c>Omission silencieuse</c>
  /// fabriquée par le service lui-même.
  /// </summary>
  [Fact]
  public void KeepsTheLineOfASystemTheCatalogueNoLongerDescribes()
  {
    var opened = ACase();

    var sheet = DeliveryLetter.Compose(opened, DataSubjectRight.Access, [], []);

    sheet.NotCovered.Count.ShouldBe(3);
    sheet.Write().ShouldContain(Agence.Value);
  }

  /// <summary>
  /// <b>Aucun chiffre.</b> Ni compte, ni taux, ni « N sur M », ni même le numéro de l'article : « 2
  /// sur 6 » reste au <c>EvidenceLog</c>, dont le lecteur est le contrôle. Le dire en cherchant un chiffre
  /// <b>partout</b> plutôt qu'en vérifiant une formule est ce qui rend la règle tenable.
  /// </summary>
  [Fact]
  public void WritesNotASingleFigure()
  {
    var opened = ACase();

    opened.LocateServed(Journal, NothingFoundIn(Journal), Opened);

    var page = DeliveryLetter.Compose(
      opened,
      DataSubjectRight.Access,
      ALandscape(),
      [APieceFrom(opened, Boutique, "nom;prénom")]).Write();

    Regex.IsMatch(page, @"\d").ShouldBeFalse($"La page de garde porte un chiffre :\n{page}");
  }

  /// <summary>
  /// <b>La deuxième liste emploie la formule exacte</b>, et invite la personne à fournir d'autres
  /// désignations. Elle ne dit <b>jamais</b> « vous n'avez rien chez nous » : un appel ne distingue
  /// pas « cherché, aucun rattachement » de « désignation insuffisante ».
  /// </summary>
  [Fact]
  public void CarriesTheDoubtOfTheSecondListRatherThanAVerdict()
  {
    var opened = ACase();

    opened.LocateServed(Journal, NothingFoundIn(Journal), Opened);

    var page = DeliveryLetter.Compose(opened, DataSubjectRight.Access, ALandscape(), []).Write();

    page.ShouldContain("sans trouver de rattachement sous les éléments dont nous disposons");
    page.ShouldContain("communiquez-la-nous");
  }

  /// <summary>
  /// <b>La page se clôt sur la non-exhaustivité du recensement.</b> Sans elle, une déclaration qui
  /// vieillit exprès prendrait l'autorité d'un recensement.
  /// </summary>
  [Fact]
  public void ClosesOnTheClauseThatTheCensusGuaranteesNothing()
  {
    var page = DeliveryLetter.Compose(ACase(), DataSubjectRight.Access, ALandscape(), []).Write();

    page.ShouldContain("systèmes recensés par le responsable de traitement");
    page.ShouldContain("ne");
    page.Trim().ShouldEndWith("garantit pas qu'il n'en existe pas d'autres.");
  }

  /// <summary>Le droit se nomme en français : « droit d'accès », jamais « article quinze ».</summary>
  [Fact]
  public void NamesTheRightInFrench()
  {
    DeliveryLetter.Compose(ACase(), DataSubjectRight.Access, ALandscape(), [])
      .Write()
      .ShouldContain("droit d'accès");
  }

  /// <summary>Un droit que le dossier ne porte pas est une programmation fautive, pas un cas de bord.</summary>
  [Fact]
  public void RefusesToComposeARightTheCaseDoesNotCarry()
  {
    Should.Throw<ArgumentException>(() =>
      DeliveryLetter.Compose(ACase(), DataSubjectRight.Erasure, ALandscape(), []));
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

  /// <summary>
  /// Trois systèmes recensés : deux joignables, et l'export mensuel que rien n'atteint — celui-là
  /// même dont la <c>DeliveryLetter</c> existe pour dire le nom.
  /// </summary>
  private static DeclaredSystem[] ALandscape()
  {
    return
    [
      ASystem(Boutique, "La boutique", "Les commandes et les comptes clients de la boutique.", reachable: true),
      ASystem(Journal, "Le journal", "Les journaux applicatifs du serveur.", reachable: true),
      ASystem(Agence, "L'export agence", AgenceContents, reachable: false),
    ];
  }

  private static DeclaredSystem ASystem(DeclaredSystemId id, string label, string contents, bool reachable)
  {
    return DeclaredSystem.Declare(
      id,
      SystemLabel.From(label),
      SystemContents.From(contents),
      reachable ? [Capability.Locate, Capability.Read] : [],
      reachable ? AdapterAddress.From("https://adapter.brocanto.example") : null,
      Opened.AddDays(-30));
  }

  private static RetrievedData APieceFrom(
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

  private static LocateFindings NothingFoundIn(DeclaredSystemId system)
  {
    return LocateFindings.ReadFrom(new LocateOnTheWire(null, null), system);
  }

  private static LocateFindings AReservationIn(DeclaredSystemId system)
  {
    return LocateFindings.ReadFrom(
      new LocateOnTheWire(
        null,
        [new ReservedOnTheWire("clients#4417", "Deux comptes portent ce nom.", null)]),
      system);
  }
}
