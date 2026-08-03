namespace MicroserviceRgpd.FunctionalTests;

/// <summary>
/// Le service demarre <b>sans moteur de verdict</b>, tel qu un poste sans materiel le demarre : la
/// configuration versionnee n allume pas le moteur LLM, et le role <c>Verdict</c> n est donc pourvu
/// par rien.
/// </summary>
/// <remarks>
/// Seul le role temoin est substitue. Laisser le role de verdict au cablage reel est ce qui donne
/// au test sa valeur : une doublure posee par-dessus prouverait la presence de cette doublure, et
/// rien du drapeau. Rien ne se pose non plus sur le fil HTTP — un role qui n existe pas n a aucun
/// appel a emettre.
/// </remarks>
public sealed class LlmOffWebApplicationFactory : CustomWebApplicationFactory<Program>
{
  protected override bool SubstitutesTheVerdictRole => false;
}

/// <summary>
/// Partage une seule instance de la fabrique eteinte — donc un seul conteneur PostgreSQL — entre les
/// classes de test qui l exercent. Elle est distincte de <see cref="WebCollection"/> parce qu un
/// hote se batit une fois : deux formes d hote demandent deux fabriques.
/// </summary>
[CollectionDefinition(Name)]
public class LlmOffWebCollection : ICollectionFixture<LlmOffWebApplicationFactory>
{
  public const string Name = "Web sans moteur LLM";
}
