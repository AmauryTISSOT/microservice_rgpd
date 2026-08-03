using System.Reflection;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.Casework.Ledger;

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
      designationCount: 2);

    entry.Case.ShouldBe(caseId);
    entry.Fact.ShouldBe(LedgerFact.CaseOpened);
    entry.OccurredAt.ShouldBe(Opened);
    entry.IdentityDeclaration.ShouldBe(IdentityDeclaration.ApplicationSession);
    entry.DesignationCount.ShouldBe(2);
  }

  /// <summary>
  /// Un appel refusé laisse une <b>tentative datée</b>, et les deux refus gardent leur distinction
  /// jusque dans la preuve : ils ne se réparent pas au même endroit, et un « appel refusé » unique
  /// ferait chercher au mauvais endroit qui relira.
  /// </summary>
  [Theory]
  [InlineData(nameof(AdapterVerdict.SecretRefused), nameof(LedgerFact.AdapterRefusedTheSecret))]
  [InlineData(nameof(AdapterVerdict.SystemNotServed), nameof(LedgerFact.AdapterDidNotServeTheSystem))]
  public void WritesTheDatedAttemptOfARefusedCall(string verdict, string expected)
  {
    var caseId = CaseId.Next();

    var entry = LedgerEntry.AdapterRefused(
      caseId,
      Opened,
      DeclaredSystemId.From("boutique"),
      AdapterVerdict.FromName(verdict));

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
  [InlineData(nameof(AdapterVerdict.Served))]
  [InlineData(nameof(AdapterVerdict.Deferred))]
  public void RefusesToWriteACallThatWasNotRefused(string verdict)
  {
    Should.Throw<ArgumentException>(() => LedgerEntry.AdapterRefused(
      CaseId.Next(),
      Opened,
      DeclaredSystemId.From("boutique"),
      AdapterVerdict.FromName(verdict)));
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

    // La seule prose du dossier qui survive est la prose de preuve, écrite à un point de décision.
    // Aucune chaîne libre n'entre ici tant qu'aucune n'a été décidée : un champ de texte non
    // qualifié serait exactement la porte par laquelle un nom finirait par passer.
    carried.ShouldNotContain(
      property => property.PropertyType == typeof(string),
      "Aucune chaîne libre sur la ligne : le seul nom permis est celui du signataire, et il vit "
      + "dans Signatory, où sa raison d'être est écrite.");
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

    var signed = Signatory.Operator("  Claire Berger  ");

    signed.Kind.ShouldBe(SignatoryKind.Operator);
    signed.Name.ShouldBe("Claire Berger");
  }

  /// <summary>Un opérateur anonyme n'existe pas : une signature vide serait une preuve qui ne prouve rien.</summary>
  [Fact]
  public void RefusesAnOperatorWhoSignedWithNothing()
  {
    Should.Throw<ArgumentException>(() => Signatory.Operator("   "));
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
      designationCount: 0);

    entry.OccurredAt.Offset.ShouldBe(TimeSpan.Zero);
    entry.OccurredAt.ShouldBe(Opened);
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
