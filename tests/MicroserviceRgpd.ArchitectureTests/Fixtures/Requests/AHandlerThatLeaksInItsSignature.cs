using MicroserviceRgpd.ArchitectureTests.Fixtures.Qualification;

namespace MicroserviceRgpd.ArchitectureTests.Fixtures.Requests;

/// <summary>
/// La fuite qu'un test de signatures seules aurait vue lui aussi. Elle est ici pour que le témoin
/// négatif du corps de méthode ne devienne pas, à lui seul, toute la couverture de l'inspecteur.
/// </summary>
internal sealed class AHandlerThatLeaksInItsSignature
{
  internal AnEngineOfTheOtherContext? Engine { get; init; }
}
