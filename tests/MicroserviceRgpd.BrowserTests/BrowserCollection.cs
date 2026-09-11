namespace MicroserviceRgpd.BrowserTests;

/// <summary>
/// Partage un seul harnais — donc un seul conteneur PostgreSQL, un seul service et un seul
/// Chromium — entre toutes les classes de test du projet.
/// </summary>
[CollectionDefinition(Name)]
public class BrowserCollection : ICollectionFixture<BrowserHarness>
{
  public const string Name = "Browser";
}
