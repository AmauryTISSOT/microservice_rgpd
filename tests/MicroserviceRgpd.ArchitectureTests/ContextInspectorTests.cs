using MicroserviceRgpd.ArchitectureTests.Fixtures.Requests;
using MicroserviceRgpd.ArchitectureTests.Fixtures.Screening;

namespace MicroserviceRgpd.ArchitectureTests;

/// <summary>
/// Ce que l'inspecteur voit vraiment. <see cref="ContextIsolationTests"/> est vert tant que la
/// production ne traverse rien d'interdit : sans ces témoins, un inspecteur qui ne verrait
/// strictement rien afficherait exactement le même vert.
/// <para>
/// L'inspecteur est donc retourné contre <b>son propre assemblage</b>, où vivent trois témoins
/// écrits pour lui : une fuite cachée dans un corps de méthode, une fuite exposée en signature, et
/// un type qui ne traverse rien.
/// </para>
/// </summary>
public class ContextInspectorTests
{
  private static readonly string ThisAssembly =
    Path.Combine(AppContext.BaseDirectory, "MicroserviceRgpd.ArchitectureTests.dll");

  private static IReadOnlyList<CrossContextReference> Crossings()
  {
    return ContextInspector.Inspect(ThisAssembly, from: ContextInspector.Requests, to: ContextInspector.Qualification);
  }

  /// <summary>La raison d'être du contrôle du code compilé, tenue par un test.</summary>
  [Fact]
  public void SeesACrossingHiddenInAMethodBody()
  {
    var crossings = Crossings();

    crossings.ShouldContain(
      crossing => crossing.SourceType == typeof(AHandlerThatLeaksInItsBody).FullName
        && crossing.Site.Contains(nameof(AHandlerThatLeaksInItsBody.Handle), StringComparison.Ordinal),
      "L'inspecteur ne lit pas les corps de méthode : la fuite qu'on craint lui échappe.");
  }

  [Fact]
  public void SeesACrossingExposedInASignature()
  {
    var crossings = Crossings();

    crossings.ShouldContain(crossing => crossing.SourceType == typeof(AHandlerThatLeaksInItsSignature).FullName);
  }

  /// <summary>
  /// Un <c>typeof</c> dans un attribut lie les deux assemblages aussi sûrement qu'un appel, et ne
  /// se voit ni dans une signature ni dans un corps de méthode.
  /// </summary>
  [Fact]
  public void SeesACrossingCarriedByAnAttributeArgument()
  {
    var crossings = Crossings();

    crossings.ShouldContain(crossing => crossing.SourceType == typeof(AHandlerThatLeaksThroughAnAttribute).FullName);
  }

  [Fact]
  public void SaysNothingOfATypeThatCrossesNothing()
  {
    var crossings = Crossings();

    crossings.ShouldNotContain(crossing => crossing.SourceType == typeof(AHandlerThatStaysHome).FullName);
  }

  /// <summary>
  /// <b>Lu d'où qu'elle parte</b>, une référence portée par un type sans contexte se voit aussi —
  /// ce que la matrice, qui ne lit que d'un contexte à un autre, ne regarde jamais.
  /// </summary>
  [Fact]
  public void SeesAReferenceCarriedByATypeWithoutAContext()
  {
    ContextInspector.ReferencesTo(ThisAssembly, ContextInspector.Requests)
      .ShouldContain(reference => reference.SourceType == typeof(Fixtures.Reporting.AnExportServiceWithoutAContext).FullName);
  }

  /// <summary>
  /// Un appel générique — <c>Select&lt;T, R&gt;</c> — se lit <b>sans faire tomber l'inspecteur</b>.
  /// C'est une panne que le garde a connue à ses débuts : la surcharge qui déballe une opérande se
  /// rappelait elle-même sans fin sur un <c>GenericInstanceMethod</c>, et le garde mourait par
  /// débordement de pile — c'est-à-dire ni en rouge ni en vert, la seule couleur qu'un garde n'a pas
  /// le droit d'avoir.
  /// </summary>
  [Fact]
  public void ReadsAGenericCallWithoutFallingOverInsteadOfShowingRedOrGreen()
  {
    var crossings = Crossings();

    crossings.ShouldNotContain(
      crossing => crossing.SourceType == typeof(AHandlerThatCallsAGenericMethod).FullName);
  }

  /// <summary>
  /// L'inspecteur est <b>orienté</b>, et il doit le rester même là où les deux sens sont interdits.
  /// Depuis <c>docs/adr/0003</c>, toute traversée non écrite est interdite, <c>Qualification →
  /// Requests</c> comme les autres ; ce
  /// qui se tient ici n'est donc plus qu'un sens soit libre, mais que l'inspecteur ne <b>confonde</b>
  /// pas les deux — sans quoi les deux seules traversées permises du dépôt, celles qui vont vers le
  /// noyau partagé, se dénonceraient elles-mêmes à l'envers.
  /// </summary>
  [Fact]
  public void ReadsTheRuleInOneDirectionOnly()
  {
    var backwards = ContextInspector.Inspect(
      ThisAssembly,
      from: ContextInspector.Qualification,
      to: ContextInspector.Requests);

    backwards.ShouldBeEmpty();
  }

  /// <summary>
  /// ⚠️ <b>Un paquet n'est pas un contexte</b>, quel que soit le mot qu'il porte dans son espace de
  /// noms. Le noyau partagé du dépôt est <c>MicroserviceRgpd.Core.SharedKernel</c> — la taxonomie
  /// écrite par le RGPD — et non le <c>Ardalis.SharedKernel</c> d'où vient le marqueur
  /// <c>IAggregateRoot</c> que les agrégats des trois contextes implémentent.
  /// <para>
  /// Le cas ne s'était jamais présenté : les deux contextes qui portaient du code ont tous deux la
  /// traversée vers le noyau <b>permise</b>, si bien que le faux positif y restait couvert par une
  /// permission légitime. <c>Screening</c>, à qui elle est refusée, est le premier à le découvrir —
  /// et il l'aurait découvert sur son premier agrégat.
  /// </para>
  /// </summary>
  [Fact]
  public void TakesNoLibraryNamespaceForAContextOfThisRepository()
  {
    var crossings = ContextInspector.Inspect(
      ThisAssembly,
      from: ContextInspector.Screening,
      to: ContextInspector.SharedKernel);

    crossings.ShouldNotContain(
      crossing => crossing.SourceType == typeof(AnAggregateCarryingALibraryMarker).FullName,
      "Ardalis.SharedKernel se lit comme le noyau partagé du dépôt : le garde dénonce un agrégat " +
      "pour avoir implémenté le marqueur de bibliothèque que les trois contextes utilisent.");
  }

  /// <summary>
  /// L'exact pendant du précédent, et il est indispensable : une borne posée sur les espaces de noms
  /// peut tout éteindre sans que rien ne passe au rouge. Le geste que <c>docs/adr/0003</c> interdit
  /// nommément — rattacher une colonne à un <c>DataSubjectRight</c> — doit rester <b>vu</b>.
  /// </summary>
  [Fact]
  public void StillSeesAScreeningTypeReachingTheRealSharedKernel()
  {
    var crossings = ContextInspector.Inspect(
      ThisAssembly,
      from: ContextInspector.Screening,
      to: ContextInspector.SharedKernel);

    crossings.ShouldContain(
      crossing => crossing.SourceType == typeof(AScreeningTypeThatReachesTheSharedKernel).FullName,
      "Screening atteint DataSubjectRight sans que le garde ne dise rien : une colonne « courriel » " +
      "ne relève pas d'un droit plutôt qu'un autre, elle relève de tous.");
  }

  /// <summary>
  /// Le témoin nomme le type atteint, pas seulement celui qui triche : un message qui n'indique
  /// pas où l'on est passé oblige à refaire l'enquête à la main.
  /// </summary>
  [Fact]
  public void NamesBothSidesOfTheCrossing()
  {
    var crossing = Crossings().First(found => found.SourceType == typeof(AHandlerThatLeaksInItsBody).FullName);

    crossing.TargetType.ShouldContain("AnEngineOfTheOtherContext");
    crossing.ToString().ShouldContain(crossing.TargetType);
  }
}
