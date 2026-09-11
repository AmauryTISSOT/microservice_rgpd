namespace MicroserviceRgpd.ArchitectureTests.Fixtures.Requests;

/// <summary>
/// Le témoin qui ne traverse rien mais qui <b>appelle une méthode générique</b> — un
/// <c>GenericInstanceMethod</c> dans l'IL, la forme d'opérande qui a un jour fait tomber
/// l'inspecteur par débordement de pile plutôt que par un rouge.
/// </summary>
/// <remarks>
/// Il est écrit ici, et non laissé au vrai code d'un contexte, parce qu'un garde ne doit pas
/// dépendre de ce qu'un contexte contienne une ligne de LINQ pour prouver qu'il tient debout.
/// </remarks>
internal sealed class AHandlerThatCallsAGenericMethod
{
  internal IReadOnlyList<string> Handle(IEnumerable<string> texts)
  {
    return [.. texts.Select(text => text.Trim()).OrderBy(text => text, StringComparer.Ordinal)];
  }
}
