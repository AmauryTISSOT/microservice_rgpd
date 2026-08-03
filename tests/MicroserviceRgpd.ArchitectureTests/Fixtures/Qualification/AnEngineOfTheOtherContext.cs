namespace MicroserviceRgpd.ArchitectureTests.Fixtures.Qualification;

/// <summary>
/// Le type « d'en face » — celui que <c>Casework</c> n'a pas le droit d'atteindre. Il ne vit ici
/// que pour donner à l'inspecteur d'IL quelque chose à trouver : le dépôt n'a pas encore de
/// <c>Casework</c>, et un garde qu'on n'a jamais vu passer au rouge ne garde rien.
/// </summary>
internal sealed class AnEngineOfTheOtherContext
{
  internal static string Qualify(string text)
  {
    return text.Trim();
  }
}
