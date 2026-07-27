namespace MicroserviceRgpd.FunctionalTests;

/// <summary>
/// Partage une seule instance de la fabrique — donc un seul conteneur PostgreSQL — entre
/// toutes les classes de test du projet.
/// </summary>
[CollectionDefinition(Name)]
public class WebCollection : ICollectionFixture<CustomWebApplicationFactory<Program>>
{
  public const string Name = "Web";
}
