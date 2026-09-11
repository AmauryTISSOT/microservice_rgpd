namespace MicroserviceRgpd.ArchitectureTests;

/// <summary>
/// <b>La couche web ne connaît plus <c>Casework</c></b> — ni ses écrans, ni sa route, ni un type
/// d'une autre couche qu'une page, un point de montage ou le layout nommerait encore.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce que la matrice ne voit pas, et que ce test voit.</b> <see cref="ContextIsolationTests"/>
/// laisse <c>Casework</c> atteindre son propre noyau et ignore les types sans contexte : un
/// <c>ServiceConfigs</c> qui enregistrerait encore un service de <c>Casework</c>, ou un layout qui
/// en lirait une constante, y resteraient verts. L'IL est donc lu ici <b>d'où que parte la
/// référence</b>.
/// </para>
/// <para>
/// Le test est <b>provisoire</b> : il existe tant que <c>Casework</c> vit dans les autres couches,
/// et part avec le nom du contexte quand le retrait atteint le domaine.
/// </para>
/// </remarks>
public class TheWebLayerKnowsNoCaseworkTests
{
  private const string Web = "MicroserviceRgpd.Web";

  [Fact]
  public void NoWebTypeLivesUnderCasework()
  {
    ContextInspector.TypesIn(ProductionAssembly.PathOf(Web), ContextInspector.Casework)
      .ShouldBeEmpty("La couche web range encore un type sous Casework.");
  }

  /// <summary>
  /// ⚠️ <b>Le conteneur que Mediator engendre est écarté, et lui seul.</b> Le générateur l'écrit dans
  /// l'assemblage qui appelle <c>AddMediator</c> et y nomme chaque gestionnaire des assemblages
  /// qu'il parcourt, ceux de <c>Casework</c> compris tant qu'ils vivent dans <c>UseCases</c>.
  /// Personne ne l'écrit, et il oublie <c>Casework</c> le jour où ces gestionnaires partent.
  /// </summary>
  [Fact]
  public void NoWebTypeReachesCasework()
  {
    var references = ContextInspector.ReferencesTo(ProductionAssembly.PathOf(Web), ContextInspector.Casework)
      .Where(reference => !IsEngenderedByMediator(reference.SourceType))
      .ToList();

    references.ShouldBeEmpty(
      $"{Web} référence encore Casework :" + Environment.NewLine +
      string.Join(Environment.NewLine, references) + Environment.NewLine +
      "Les écrans et la route de Casework sont retirés : ce qui reste de Casework dans les autres " +
      "couches se compose hors de la couche web — voir UseCasesServiceExtensions.");
  }

  /// <summary>
  /// <b>Le témoin</b> : retourné contre son propre assemblage, l'inspecteur voit la référence que
  /// porte un type sans contexte. Sans lui, un inspecteur qui ne verrait rien afficherait le même
  /// vert que ci-dessus.
  /// </summary>
  [Fact]
  public void SeesAReferenceCarriedByATypeWithoutAContext()
  {
    var thisAssembly = Path.Combine(AppContext.BaseDirectory, "MicroserviceRgpd.ArchitectureTests.dll");

    ContextInspector.ReferencesTo(thisAssembly, ContextInspector.Requests)
      .ShouldContain(reference => reference.SourceType == typeof(Fixtures.Reporting.AnExportServiceWithoutAContext).FullName);
  }

  private static bool IsEngenderedByMediator(string type)
  {
    return type.StartsWith("Mediator.", StringComparison.Ordinal)
      || type == "Microsoft.Extensions.DependencyInjection.MediatorDependencyInjectionExtensions";
  }
}
