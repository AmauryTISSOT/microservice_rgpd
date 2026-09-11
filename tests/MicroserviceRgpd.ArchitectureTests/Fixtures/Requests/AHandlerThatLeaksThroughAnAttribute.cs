using MicroserviceRgpd.ArchitectureTests.Fixtures.Qualification;

namespace MicroserviceRgpd.ArchitectureTests.Fixtures.Requests;

/// <summary>
/// La fuite par <c>typeof</c> : elle n'est ni dans une signature ni dans un corps de méthode, mais
/// dans l'argument d'un attribut — et elle lie tout autant les deux assemblages. C'est ainsi qu'un
/// <c>[JsonConverter(typeof(…))]</c> ou un <c>[JsonDerivedType(typeof(…))]</c> traverserait.
/// </summary>
[Consults(typeof(AnEngineOfTheOtherContext))]
internal sealed class AHandlerThatLeaksThroughAnAttribute
{
  internal string Handle(string text)
  {
    return text.Trim();
  }
}
