using MicroserviceRgpd.ArchitectureTests.Fixtures.Qualification;

namespace MicroserviceRgpd.ArchitectureTests.Fixtures.Casework;

/// <summary>
/// La fuite même que l'on craint, réduite à sa plus simple expression : <b>rien</b> dans la surface
/// de ce type ne traverse la frontière — ni son type de base, ni ses champs, ni les paramètres ou
/// le type de retour de sa méthode. Tout est dans le corps.
/// <para>
/// C'est exactement ce sur quoi un test de signatures seules afficherait vert. Il sert de témoin :
/// tant que l'inspecteur le voit, on sait que le garde lit bien l'IL.
/// </para>
/// </summary>
internal sealed class AHandlerThatLeaksInItsBody
{
  internal string Handle(string text)
  {
    return AnEngineOfTheOtherContext.Qualify(text);
  }
}
