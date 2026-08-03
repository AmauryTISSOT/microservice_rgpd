using MicroserviceRgpd.ArchitectureTests.Fixtures.Casework;

namespace MicroserviceRgpd.ArchitectureTests;

/// <summary>
/// Ce que l'inspecteur voit vraiment. <see cref="ContextIsolationTests"/> ne trouve rien à examiner
/// tant que <c>Casework</c> n'a pas de code : sans ces témoins, un inspecteur qui ne verrait
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
    return ContextInspector.Inspect(ThisAssembly, from: ContextInspector.Casework, to: ContextInspector.Qualification);
  }

  /// <summary>La raison d'être du contrôle au niveau de l'IL, tenue par un test.</summary>
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

  [Fact]
  public void SaysNothingOfATypeThatCrossesNothing()
  {
    var crossings = Crossings();

    crossings.ShouldNotContain(crossing => crossing.SourceType == typeof(AHandlerThatStaysHome).FullName);
  }

  /// <summary>
  /// La règle est orientée : <c>Qualification</c> n'a pas le droit d'atteindre <c>Casework</c> non
  /// plus, mais ce n'est pas cette règle-ci, et un inspecteur qui confondrait les deux sens
  /// dénoncerait des dépendances légitimes le jour où l'autre sens s'ouvrirait.
  /// </summary>
  [Fact]
  public void ReadsTheRuleInOneDirectionOnly()
  {
    var backwards = ContextInspector.Inspect(
      ThisAssembly,
      from: ContextInspector.Qualification,
      to: ContextInspector.Casework);

    backwards.ShouldBeEmpty();
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
