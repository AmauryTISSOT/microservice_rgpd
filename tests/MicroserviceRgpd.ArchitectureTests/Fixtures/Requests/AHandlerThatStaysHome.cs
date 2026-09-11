namespace MicroserviceRgpd.ArchitectureTests.Fixtures.Requests;

/// <summary>
/// Le témoin négatif : un type de <c>Requests</c> qui ne traverse rien. Sans lui, un inspecteur
/// qui dénoncerait tout le monde passerait pour un inspecteur qui marche.
/// </summary>
internal sealed class AHandlerThatStaysHome
{
  internal string Handle(string text)
  {
    return text.Trim();
  }
}
