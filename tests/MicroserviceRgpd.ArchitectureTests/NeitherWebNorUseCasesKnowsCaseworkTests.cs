namespace MicroserviceRgpd.ArchitectureTests;

/// <summary>
/// <b>Ni la couche web ni les cas d'usage ne connaissent plus <c>Casework</c></b> — ni ses écrans,
/// ni sa route, ni ses gestionnaires, ni un type d'une autre couche qu'une page, un point de montage
/// ou le layout nommerait encore.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce que la matrice ne voit pas, et que ce test voit.</b> <see cref="ContextIsolationTests"/>
/// laisse <c>Casework</c> atteindre son propre noyau et ignore les types sans contexte : un
/// <c>ServiceConfigs</c> qui enregistrerait encore un service de <c>Casework</c>, un gestionnaire de
/// <c>Casework</c> resté dans <c>UseCases</c>, ou un layout qui en lirait une constante, y resteraient
/// verts. L'IL est donc lu ici <b>d'où que parte la référence</b>.
/// </para>
/// <para>
/// ⚠️ <b>Aucune exemption pour le conteneur que Mediator engendre.</b> Le générateur l'écrit dans
/// l'assemblage qui appelle <c>AddMediator</c> et y nomme chaque gestionnaire des assemblages qu'il
/// parcourt. Depuis que les gestionnaires de <c>Casework</c> ont quitté <c>UseCases</c>, il n'en nomme
/// plus aucun : une référence qu'il porterait encore dirait qu'un gestionnaire est revenu.
/// </para>
/// <para>
/// Le test est <b>provisoire</b> : il existe tant que <c>Casework</c> vit dans les autres couches,
/// et part avec le nom du contexte quand le retrait atteint le domaine.
/// </para>
/// </remarks>
public class NeitherWebNorUseCasesKnowsCaseworkTests
{
  [Theory]
  [InlineData("MicroserviceRgpd.Web")]
  [InlineData("MicroserviceRgpd.UseCases")]
  public void NoTypeLivesUnderCasework(string assembly)
  {
    ContextInspector.TypesIn(ProductionAssembly.PathOf(assembly), ContextInspector.Casework)
      .ShouldBeEmpty($"{assembly} range encore un type sous Casework.");
  }

  [Theory]
  [InlineData("MicroserviceRgpd.Web")]
  [InlineData("MicroserviceRgpd.UseCases")]
  public void NoTypeReachesCasework(string assembly)
  {
    var references = ContextInspector.ReferencesTo(ProductionAssembly.PathOf(assembly), ContextInspector.Casework);

    references.ShouldBeEmpty(
      $"{assembly} référence encore Casework :" + Environment.NewLine +
      string.Join(Environment.NewLine, references) + Environment.NewLine +
      "Les écrans, la route et les cas d'usage de Casework sont retirés : seuls Infrastructure et " +
      "Core le portent encore, jusqu'à leur propre retrait.");
  }
}
