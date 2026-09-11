using MicroserviceRgpd.ArchitectureTests.Fixtures.Requests;

namespace MicroserviceRgpd.ArchitectureTests.Fixtures.Reporting;

/// <summary>
/// Le témoin négatif : un type <b>sans contexte</b> lui aussi, qui n'en atteint qu'un.
/// </summary>
/// <remarks>
/// Sans lui, un garde qui dénoncerait tout fichier sans contexte passerait pour un garde qui
/// marche — et il faudrait alors ranger dans une liste blanche la moitié de la couche
/// d'infrastructure. La règle porte sur le <b>rapprochement</b> de deux contextes, jamais sur
/// l'absence de contexte : un adaptateur qui sert un seul contexte n'a rien à se reprocher, où
/// qu'il vive.
/// </remarks>
internal sealed class AReaderThatStaysWithinOneContext
{
  internal static string Read()
  {
    return new AHandlerThatStaysHome().Handle(" ");
  }
}
