using System.Reflection;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.Casework.Ledger;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UnitTests.Core.Casework;

/// <summary>
/// Ce que le <c>Ledger</c> sait écrire, et ce qu'il est <b>incapable</b> d'écrire.
/// <para>
/// Ces tests portent sur la <b>forme des types</b> plutôt que sur un comportement, et c'est
/// délibéré : « anonyme par construction, jamais par expurgation » est une promesse qu'on ne peut
/// pas tenir en vérifiant ce qui a été écrit — il faudrait avoir tout écrit pour le savoir. On
/// vérifie qu'il n'existe <b>aucun emplacement</b> où une désignation pourrait atterrir.
/// </para>
/// </summary>
public class LedgerEntryTests
{
  private static readonly DateTimeOffset Opened = new(2026, 8, 3, 14, 30, 0, TimeSpan.Zero);

  /// <summary>
  /// La première ligne dit le fait, sa date, son signataire, la déclaration d'identité en vigueur
  /// et le <b>nombre</b> de désignations — jamais lesquelles.
  /// </summary>
  [Fact]
  public void WritesTheOpeningOfACaseWithoutASingleDesignation()
  {
    var caseId = CaseId.Next();

    var entry = LedgerEntry.CaseOpened(
      caseId,
      Opened,
      Signatory.Application,
      IdentityDeclaration.ApplicationSession,
      designationCount: 2,
      reception: ReceptionDate.Declared(Opened));

    entry.Case.ShouldBe(caseId);
    entry.Fact.ShouldBe(LedgerFact.CaseOpened);
    entry.OccurredAt.ShouldBe(Opened);
    entry.IdentityDeclaration.ShouldBe(IdentityDeclaration.ApplicationSession);
    entry.DesignationCount.ShouldBe(2);
  }

  /// <summary>
  /// <b>Une date de réception tenue pour défaut s'inscrit comme un défaut.</b> Sans ce fait, la
  /// preuve garderait la même trace d'une date affirmée par un humain et d'une hypothèse du service,
  /// et personne ne saurait plus laquelle des deux il relit dix ans après.
  /// </summary>
  [Fact]
  public void WritesADefaultedReceptionDateAsADefaultRatherThanAsADeclaredFact()
  {
    var defaulted = LedgerEntry.CaseOpened(
      CaseId.Next(),
      Opened,
      Signatory.Application,
      IdentityDeclaration.Unverified,
      designationCount: 1,
      reception: ReceptionDate.Defaulted(Opened));

    var declared = LedgerEntry.CaseOpened(
      CaseId.Next(),
      Opened,
      Signatory.Application,
      IdentityDeclaration.Unverified,
      designationCount: 1,
      reception: ReceptionDate.Declared(Opened));

    defaulted.ReceptionWasDefaulted.ShouldBe(true);
    declared.ReceptionWasDefaulted.ShouldBe(false);
  }

  /// <summary>
  /// Un constat déclaré : l'état, le système, le droit, le nom de l'humain, <b>le régime sous lequel
  /// il a saisi ce nom</b>, et sa prose de preuve.
  /// </summary>
  [Fact]
  public void WritesAFindingUnderTheNameAndTheRegimeUnderWhichItWasTyped()
  {
    var entry = LedgerEntry.StepDeclared(
      CaseId.Next(),
      Opened,
      DeclaredSystemId.From("boutique"),
      DataSubjectRight.Access,
      StepState.Done,
      "Requête lancée le 3, deux comptes trouvés, export joint.",
      findingIsDemanded: true,
      Signatory.Operator("Claire Berger", SignatureRegime.Unauthenticated));

    entry.Fact.ShouldBe(LedgerFact.StepDeclared);
    entry.DeclaredSystem.ShouldBe(DeclaredSystemId.From("boutique"));
    entry.Right.ShouldBe(DataSubjectRight.Access);
    entry.DeclaredState.ShouldBe(StepState.Done);
    entry.EvidenceProse.ShouldBe("Requête lancée le 3, deux comptes trouvés, export joint.");

    entry.Signatory.Name.ShouldBe("Claire Berger");
    entry.Signatory.Regime.ShouldBe(SignatureRegime.Unauthenticated);

    // Ce fait ne mesure aucune recherche : un compte se lirait comme une ampleur qu'il n'a pas.
    entry.DesignationCount.ShouldBeNull();
  }

  /// <summary>
  /// <b><c>Untreated</c> s'inscrit aussi volontiers que <c>Done</c>.</b> C'est l'aveu que personne ne
  /// l'a fait, la trace la plus précieuse du dispositif, et le service n'a jamais le droit de la
  /// bloquer.
  /// </summary>
  [Fact]
  public void NeverRefusesTheAdmissionThatNobodyDidTheWork()
  {
    var entry = LedgerEntry.StepDeclared(
      CaseId.Next(),
      Opened,
      DeclaredSystemId.From("export-agence"),
      DataSubjectRight.Access,
      StepState.Untreated,
      "L'export part chez l'agence ; personne ne l'a traité pour cette demande.",
      findingIsDemanded: false,
      Signatory.Operator("Claire Berger", SignatureRegime.Unauthenticated));

    entry.DeclaredState.ShouldBe(StepState.Untreated);
  }

  /// <summary>
  /// <b>Le constat est exigé là où le dossier le réclame</b> — sur un « fait » à zéro rattachement, et
  /// par la même règle dont l'écran se sert pour le réclamer. Un <c>Done</c> coché sans un mot serait
  /// une preuve qui dit ce qui a été coché et non ce qui a été constaté, or c'est le constat que le
  /// contrôle vient lire.
  /// </summary>
  [Fact]
  public void RefusesAWorkDeclaredDoneThatNobodyMotivated()
  {
    StepState.Done.DemandsAFinding(anAttachmentIsHeld: false).ShouldBeTrue();

    Should.Throw<ArgumentException>(() => LedgerEntry.StepDeclared(
      CaseId.Next(),
      Opened,
      DeclaredSystemId.From("boutique"),
      DataSubjectRight.Access,
      StepState.Done,
      "   ",
      findingIsDemanded: true,
      Signatory.Operator("Claire Berger", SignatureRegime.Unauthenticated)));
  }

  /// <summary>
  /// <b>Un « fait » sur un système où le <c>Locate</c> a rattaché quelque chose ne réclame plus de
  /// constat.</b> Le rattachement <b>est</b> le dénominateur que le constat devait fournir : c'est
  /// six zéros qu'on ne veut pas voir se lire « cette personne n'est pas chez nous », pas un travail
  /// dont on sait ce qu'il portait.
  /// </summary>
  [Fact]
  public void AsksForNoFindingWhereTheServiceHoldsAnAttachment()
  {
    StepState.Done.DemandsAFinding(anAttachmentIsHeld: true).ShouldBeFalse();

    var entry = LedgerEntry.StepDeclared(
      CaseId.Next(),
      Opened,
      DeclaredSystemId.From("boutique"),
      DataSubjectRight.Access,
      StepState.Done,
      evidenceProse: null,
      findingIsDemanded: false,
      Signatory.Operator("Claire Berger", SignatureRegime.Unauthenticated));

    entry.EvidenceProse.ShouldBeNull();
  }

  /// <summary>
  /// <b>Ailleurs, le constat est accueilli sans être exigé.</b> Exiger une prose sur chaque état ferait
  /// écrire une ligne de rien à chaque clic, et le constat qui compte — celui d'un « fait » sans
  /// rattachement — se noierait dans les autres.
  /// </summary>
  [Theory]
  [InlineData(nameof(StepState.ToDo))]
  [InlineData(nameof(StepState.Awaiting))]
  [InlineData(nameof(StepState.OutOfReach))]
  [InlineData(nameof(StepState.Untreated))]
  public void AsksForNoFindingOnAStateThatDoesNotClaimOne(string state)
  {
    var entry = LedgerEntry.StepDeclared(
      CaseId.Next(),
      Opened,
      DeclaredSystemId.From("boutique"),
      DataSubjectRight.Access,
      StepState.FromName(state),
      evidenceProse: null,
      findingIsDemanded: false,
      Signatory.Operator("Claire Berger", SignatureRegime.Unauthenticated));

    // La colonne reste vide plutôt que de porter une chaîne vide, qui se lirait comme un constat
    // qu'on aurait effacé.
    entry.EvidenceProse.ShouldBeNull();
  }

  /// <summary>
  /// Un constat est le geste d'un humain, par définition : c'est lui qui a regardé. Le laisser signer
  /// par l'application ferait porter à personne une déclaration que quelqu'un a faite.
  /// </summary>
  [Fact]
  public void RefusesAFindingThatNoHumanSigned()
  {
    Should.Throw<ArgumentException>(() => LedgerEntry.StepDeclared(
      CaseId.Next(),
      Opened,
      DeclaredSystemId.From("boutique"),
      DataSubjectRight.Access,
      StepState.Done,
      "Un constat sans personne pour l'avoir fait.",
      findingIsDemanded: true,
      Signatory.Application));
  }

  /// <summary>
  /// Un appel refusé laisse une <b>tentative datée</b>, et les deux refus gardent leur distinction
  /// jusque dans la preuve : ils ne se réparent pas au même endroit, et un « appel refusé » unique
  /// ferait chercher au mauvais endroit qui relira.
  /// </summary>
  [Theory]
  [InlineData(nameof(AdapterOutcome.SecretRefused), nameof(LedgerFact.AdapterRefusedTheSecret))]
  [InlineData(nameof(AdapterOutcome.SystemNotServed), nameof(LedgerFact.AdapterDidNotServeTheSystem))]
  public void WritesTheDatedAttemptOfARefusedCall(string outcome, string expected)
  {
    var caseId = CaseId.Next();

    var entry = LedgerEntry.AdapterRefused(
      caseId,
      Opened,
      DeclaredSystemId.From("boutique"),
      AdapterOutcome.FromName(outcome));

    entry.Case.ShouldBe(caseId);
    entry.Fact.ShouldBe(LedgerFact.FromName(expected));
    entry.OccurredAt.ShouldBe(Opened);
    entry.DeclaredSystem.ShouldBe(DeclaredSystemId.From("boutique"));

    // Aucun humain n'a signé : un appel sortant n'est le geste de personne, et lui donner une
    // signature d'Operator ferait porter à quelqu'un un refus qu'il n'a pas prononcé.
    entry.Signatory.ShouldBe(Signatory.Application);

    // Un refus n'a rien cherché : un zéro se lirait comme une recherche menée sous rien.
    entry.DesignationCount.ShouldBeNull();
    entry.IdentityDeclaration.ShouldBeNull();
  }

  /// <summary>
  /// Le <c>Ledger</c> ne consigne ici que des <b>refus</b>. Un appel servi ou différé n'a pas de
  /// fait à lui : ce qu'il devient appartient au dossier, pas à la preuve du transport.
  /// </summary>
  [Theory]
  [InlineData(nameof(AdapterOutcome.Served))]
  [InlineData(nameof(AdapterOutcome.Deferred))]
  public void RefusesToWriteACallThatWasNotRefused(string outcome)
  {
    Should.Throw<ArgumentException>(() => LedgerEntry.AdapterRefused(
      CaseId.Next(),
      Opened,
      DeclaredSystemId.From("boutique"),
      AdapterOutcome.FromName(outcome)));
  }

  /// <summary>
  /// <b>Aucun emplacement pour une <c>Designation</c> ni pour un nom de personne concernée</b>,
  /// dès la première ligne. La règle tient par la forme du type : ce n'est pas qu'on n'y écrit
  /// rien, c'est qu'on n'y peut rien écrire.
  /// </summary>
  [Fact]
  public void OffersNoPlaceWhereADesignationCouldEverLand()
  {
    var carried = typeof(LedgerEntry)
      .GetProperties(BindingFlags.Public | BindingFlags.Instance)
      .ToArray();

    carried.ShouldNotContain(
      property => property.PropertyType == typeof(Designation)
                  || property.PropertyType == typeof(DesignationKind)
                  || property.PropertyType.IsAssignableTo(typeof(IEnumerable<Designation>)),
      "Le Ledger n'accepte aucune Designation, dès la première ligne.");

    // La seule prose du dossier qui survive est la prose de preuve, écrite à un point de décision et
    // non nominative par nature. Les chaînes de la ligne sont donc énumérées en toutes lettres : en
    // ajouter une doit être un geste délibéré, parce que c'est par un champ de texte non qualifié
    // qu'un nom de personne concernée finirait par passer — et la prose de *travail*, qui nomme des
    // tiers, n'a aucun emplacement ici.
    carried
      .Where(property => property.PropertyType == typeof(string))
      .Select(property => property.Name)
      .ShouldBe([nameof(LedgerEntry.EvidenceProse)]);
  }

  /// <summary>
  /// <b>Le seul nom permis est celui de l'<c>Operator</c></b>, et « personne n'a signé » s'écrit
  /// comme un fait plutôt que comme un nom manquant.
  /// </summary>
  [Fact]
  public void NamesTheOperatorAndSaysSoWhenNoHumanSigned()
  {
    Signatory.Application.Kind.ShouldBe(SignatoryKind.Application);
    Signatory.Application.Name.ShouldBeNull();

    var signed = Signatory.Operator("  Claire Berger  ", SignatureRegime.Unauthenticated);

    signed.Kind.ShouldBe(SignatoryKind.Operator);
    signed.Name.ShouldBe("Claire Berger");
  }

  /// <summary>Un opérateur anonyme n'existe pas : une signature vide serait une preuve qui ne prouve rien.</summary>
  [Fact]
  public void RefusesAnOperatorWhoSignedWithNothing()
  {
    Should.Throw<ArgumentException>(() => Signatory.Operator("   ", SignatureRegime.Unauthenticated));
  }

  /// <summary>
  /// <b>Un nom n'est pas une authentification, et le régime le dit.</b> Il s'écrit en même temps que
  /// le nom et par le même geste : une signature qui ne garderait que le nom saisi serait relue dans
  /// dix ans comme si quelqu'un s'était identifié.
  /// <para>
  /// Une seule valeur existe aujourd'hui, et c'est la raison d'être du type : le jour où la GUI
  /// authentifiera son <c>Operator</c>, les lignes d'hier resteront lisibles pour ce qu'elles sont.
  /// </para>
  /// </summary>
  [Fact]
  public void MarksTheRegimeUnderWhichTheNameWasTypedAtTheSameTimeAsTheName()
  {
    // Le régime n'a pas de valeur par défaut : un défaut ferait écrire « non authentifié » par oubli
    // le jour où l'authentification existera.
    typeof(Signatory).GetMethod(nameof(Signatory.Operator))!
      .GetParameters()
      .ShouldNotContain(parameter => parameter.HasDefaultValue);

    Signatory.Operator("Claire Berger", SignatureRegime.Unauthenticated).Regime
      .ShouldBe(SignatureRegime.Unauthenticated);

    // L'application n'a saisi aucun nom : un régime de signature ne dirait rien d'elle.
    Signatory.Application.Regime.ShouldBeNull();
  }

  /// <summary>
  /// L'instant est ramené en UTC : <c>timestamptz</c> ne conserve pas le décalage, et laisser
  /// passer une heure locale ferait dépendre la preuve du fuseau de la machine qui l'a écrite.
  /// </summary>
  [Fact]
  public void BringsTheInstantBackToUtcRatherThanTrustingTheMachinesTimeZone()
  {
    var entry = LedgerEntry.CaseOpened(
      CaseId.Next(),
      new DateTimeOffset(2026, 8, 3, 16, 30, 0, TimeSpan.FromHours(2)),
      Signatory.Application,
      IdentityDeclaration.ApplicationSession,
      designationCount: 0,
      reception: ReceptionDate.Declared(Opened));

    entry.OccurredAt.Offset.ShouldBe(TimeSpan.Zero);
    entry.OccurredAt.ShouldBe(Opened);
  }

  /// <summary>
  /// La remise déclarée dit le droit, le jour, le signataire et <b>le « 2 sur 6 »</b> — jamais ce
  /// qu'il y avait dans le fichier remis.
  /// </summary>
  [Fact]
  public void WritesWhatTheHandedAnswerCoveredWithoutSayingWhatWasInIt()
  {
    var caseId = CaseId.Next();

    var entry = LedgerEntry.DeliveryDeclared(
      caseId,
      Opened,
      DataSubjectRight.Access,
      coveredSystemCount: 2,
      declaredSystemCount: 6,
      Signatory.Operator("Camille Roy", SignatureRegime.Unauthenticated));

    entry.Case.ShouldBe(caseId);
    entry.Fact.ShouldBe(LedgerFact.DeliveryDeclared);
    entry.OccurredAt.ShouldBe(Opened);
    entry.Right.ShouldBe(DataSubjectRight.Access);
    entry.CoveredSystemCount.ShouldBe(2);
    entry.DeclaredSystemCount.ShouldBe(6);
    entry.Signatory.Name.ShouldBe("Camille Roy");

    // Rien du fichier : ni système, ni prose, ni compte de désignations. La preuve dit qu'un fichier
    // a été remis, jamais ce qu'il y avait dedans.
    entry.DeclaredSystem.ShouldBeNull();
    entry.EvidenceProse.ShouldBeNull();
    entry.DesignationCount.ShouldBeNull();
  }

  /// <summary>
  /// <b>Aucune machine n'affirme qu'une réponse a été rendue à quelqu'un.</b> La remise est une
  /// affirmation : la signer par l'application aurait fait produire au service la preuve d'un geste
  /// que personne n'a fait.
  /// </summary>
  [Fact]
  public void RefusesAHandoverThatNoOneSigned()
  {
    Should.Throw<ArgumentException>(() => LedgerEntry.DeliveryDeclared(
      CaseId.Next(),
      Opened,
      DataSubjectRight.Access,
      coveredSystemCount: 2,
      declaredSystemCount: 6,
      Signatory.Application));

    Should.Throw<ArgumentException>(() => LedgerEntry.DeliveryDeclared(
      CaseId.Next(),
      Opened,
      DataSubjectRight.Access,
      coveredSystemCount: 2,
      declaredSystemCount: 6,
      Signatory.Operator(" ", SignatureRegime.Unauthenticated)));
  }

  /// <summary>
  /// <b>« 6 sur 5 » n'est pas un rapport.</b> C'est le signe que les deux moitiés ont été comptées
  /// sur deux ensembles différents, et la preuve refuse de porter un chiffre que personne ne peut
  /// lire.
  /// </summary>
  [Fact]
  public void RefusesToCoverMoreSystemsThanTheAnswerHadToAnswerFor()
  {
    Should.Throw<ArgumentOutOfRangeException>(() => LedgerEntry.DeliveryDeclared(
      CaseId.Next(),
      Opened,
      DataSubjectRight.Access,
      coveredSystemCount: 6,
      declaredSystemCount: 5,
      Signatory.Operator("Camille Roy", SignatureRegime.Unauthenticated)));
  }

  /// <summary>
  /// <b>Le port n'expose qu'un ajout.</b> Ni mise à jour, ni suppression ligne à ligne, ni
  /// relecture : ce que le type ne sait pas faire, personne n'aura à jurer qu'il ne l'a pas fait.
  /// </summary>
  [Fact]
  public void ExposesNothingButAnAppend()
  {
    typeof(ILedger).GetMethods().Select(method => method.Name).ShouldBe(["AppendAsync"]);
  }

  /// <summary>
  /// <b>Rien de l'écriture ne dépend du contenu d'une ligne antérieure.</b> Écrire une ligne ne
  /// demande aucune ligne — ni la précédente, ni un rang, ni un total courant : une écriture qui
  /// lirait la ligne d'avant serait une écriture qu'une ligne d'avant pourrait faire mentir.
  /// </summary>
  [Fact]
  public void NeedsNoEarlierLineToWriteALine()
  {
    var append = typeof(ILedger).GetMethod(nameof(ILedger.AppendAsync))!;

    append.GetParameters()
      .Select(parameter => parameter.ParameterType)
      .ShouldBe([typeof(LedgerEntry), typeof(CancellationToken)]);

    // Aucune fabrique de ligne ne prend de ligne : le rang n'existe pas, et l'identité de la ligne
    // est engendrée plutôt que comptée.
    typeof(LedgerEntry)
      .GetMethods(BindingFlags.Public | BindingFlags.Static)
      // Les opérateurs d'égalité qu'un `record` engendre comparent deux lignes ; ils n'en écrivent
      // aucune, et ce sont les fabriques qu'on lit ici.
      .Where(factory => !factory.IsSpecialName)
      .SelectMany(factory => factory.GetParameters())
      .ShouldNotContain(parameter => parameter.ParameterType == typeof(LedgerEntry));
  }
}
