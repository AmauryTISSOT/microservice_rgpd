using Ardalis.Result;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Vogen;

namespace MicroserviceRgpd.Web.Pages.Casework;

/// <summary>
/// La frontière entre ce qu'un humain saisit et ce que le domaine accepte : <b>le type lève, la
/// frontière nomme</b>.
/// </summary>
/// <remarks>
/// <para>
/// Les deux gestes vivent ici une seule fois, parce que tous les écrans de ce contexte les font :
/// faire franchir une valeur, et redire à l'humain sous le nom de son champ ce qui a été refusé. Deux
/// copies finiraient par déposer leurs refus sous deux conventions de nom, et un même refus
/// s'afficherait à côté de sa case sur un écran et nulle part sur l'autre.
/// </para>
/// <para>
/// <b>Les messages viennent des types du domaine, jamais d'une seconde rédaction.</b> Deux libellés
/// pour une même règle finiraient par ne plus dire la même chose, et l'écran mentirait sur ce que la
/// base accepte.
/// </para>
/// </remarks>
internal static class FormBoundary
{
  /// <summary>
  /// Une valeur du domaine, ou le refus du type déposé sous le nom du champ.
  /// </summary>
  /// <param name="modelState">L'endroit où les refus se déposent.</param>
  /// <param name="prefix">Le préfixe de liaison du formulaire, tel que la page l'a déclaré.</param>
  /// <param name="field">Le champ que le refus doit nommer.</param>
  /// <param name="cross">Le franchissement à tenter.</param>
  internal static T? Read<T>(ModelStateDictionary modelState, string prefix, string field, Func<T> cross)
    where T : struct
  {
    try
    {
      return cross();
    }
    catch (ValueObjectValidationException refusal)
    {
      modelState.AddModelError($"{prefix}.{field}", refusal.Message);

      return null;
    }
  }

  /// <summary>
  /// Redit à l'humain, sous le nom du champ fautif, ce qu'un gestionnaire a refusé.
  /// </summary>
  /// <param name="modelState">L'endroit où les refus se déposent.</param>
  /// <param name="prefix">Le préfixe de liaison du formulaire, tel que la page l'a déclaré.</param>
  /// <param name="refusals">Ce que le gestionnaire a refusé.</param>
  internal static void Deposit(
    ModelStateDictionary modelState,
    string prefix,
    IEnumerable<ValidationError> refusals)
  {
    ArgumentNullException.ThrowIfNull(modelState);
    ArgumentNullException.ThrowIfNull(refusals);

    foreach (var refusal in refusals)
    {
      modelState.AddModelError($"{prefix}.{refusal.Identifier}", refusal.ErrorMessage);
    }
  }
}
