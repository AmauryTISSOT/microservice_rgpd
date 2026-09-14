namespace MicroserviceRgpd.FunctionalTests;

/// <summary>
/// Un hôte comme les autres, dont la base n'est partagée avec aucune autre classe de test : la
/// migration qui vide l'historique s'y rejoue sans emporter les rapports des autres tests.
/// </summary>
public sealed class AnEmptiedHistoryWebApplicationFactory : CustomWebApplicationFactory<Program>;

/// <summary>La collection de <see cref="AnEmptiedHistoryWebApplicationFactory"/>.</summary>
[CollectionDefinition(Name)]
public class AnEmptiedHistoryWebCollection : ICollectionFixture<AnEmptiedHistoryWebApplicationFactory>
{
  public const string Name = "Web à l'historique vidé par la migration";
}
