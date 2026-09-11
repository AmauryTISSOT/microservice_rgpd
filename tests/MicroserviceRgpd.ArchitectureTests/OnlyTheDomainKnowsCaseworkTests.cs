namespace MicroserviceRgpd.ArchitectureTests;

/// <summary>
/// <b>Seul le domaine connaît encore <c>Casework</c></b> : ni la couche web, ni les cas d'usage, ni
/// l'infrastructure n'en portent un type ou n'en nomment un — ni écran, ni route, ni gestionnaire, ni
/// adaptateur, ni configuration d'entité, ni <c>DbSet</c>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce que la matrice ne voit pas, et que ce test voit.</b> <see cref="ContextIsolationTests"/>
/// laisse <c>Casework</c> atteindre son propre noyau et ignore les types sans contexte : un
/// <c>ServiceConfigs</c> qui enregistrerait encore un service de <c>Casework</c>, un gestionnaire de
/// <c>Casework</c> resté dans <c>UseCases</c>, ou un <c>AppDbContext</c> qui en garderait un
/// <c>DbSet</c>, y resteraient verts. L'IL est donc lu ici <b>d'où que parte la référence</b>.
/// </para>
/// <para>
/// ⚠️ <b>Aucune exemption pour le conteneur que Mediator engendre.</b> Le générateur l'écrit dans
/// l'assemblage qui appelle <c>AddMediator</c> et y nomme chaque gestionnaire des assemblages qu'il
/// parcourt. Depuis que les gestionnaires de <c>Casework</c> ont quitté <c>UseCases</c>, il n'en nomme
/// plus aucun : une référence qu'il porterait encore dirait qu'un gestionnaire est revenu.
/// </para>
/// <para>
/// ⚠️ <b>Les migrations déjà appliquées ne font pas rougir ce test</b>, et c'est juste : elles nomment
/// les types de <c>Casework</c> par des chaînes, jamais par une référence de type. L'IL n'en voit donc
/// rien, et elles restent intactes.
/// </para>
/// <para>
/// Le test est <b>provisoire</b> : il existe tant que <c>Casework</c> vit dans <c>Core</c>, et part
/// avec le nom du contexte quand le retrait atteint le domaine.
/// </para>
/// </remarks>
public class OnlyTheDomainKnowsCaseworkTests
{
  [Theory]
  [InlineData("MicroserviceRgpd.Web")]
  [InlineData("MicroserviceRgpd.UseCases")]
  [InlineData("MicroserviceRgpd.Infrastructure")]
  public void NoTypeLivesUnderCasework(string assembly)
  {
    ContextInspector.TypesIn(ProductionAssembly.PathOf(assembly), ContextInspector.Casework)
      .ShouldBeEmpty($"{assembly} range encore un type sous Casework.");
  }

  [Theory]
  [InlineData("MicroserviceRgpd.Web")]
  [InlineData("MicroserviceRgpd.UseCases")]
  [InlineData("MicroserviceRgpd.Infrastructure")]
  public void NoTypeReachesCasework(string assembly)
  {
    var references = ContextInspector.ReferencesTo(ProductionAssembly.PathOf(assembly), ContextInspector.Casework);

    references.ShouldBeEmpty(
      $"{assembly} référence encore Casework :" + Environment.NewLine +
      string.Join(Environment.NewLine, references) + Environment.NewLine +
      "Les écrans, la route, les cas d'usage et l'infrastructure de Casework sont retirés : seul " +
      "Core le porte encore, jusqu'à son propre retrait.");
  }
}
