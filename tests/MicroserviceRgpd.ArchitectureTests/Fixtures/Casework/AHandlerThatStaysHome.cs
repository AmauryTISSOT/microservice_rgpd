namespace MicroserviceRgpd.ArchitectureTests.Fixtures.Casework;

/// <summary>
/// Le témoin négatif : un type de <c>Casework</c> qui ne traverse rien. Sans lui, un inspecteur
/// qui dénoncerait tout le monde passerait pour un inspecteur qui marche.
/// </summary>
internal sealed class AHandlerThatStaysHome
{
  internal string Handle(string text)
  {
    return text.Trim();
  }
}
