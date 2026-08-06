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
  /// Une valeur d'un vocabulaire <b>fermé</b>, ou le refus déposé sous le nom du champ.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Le vide entre comme vide, jamais comme un nul.</b> Les listes de l'écran sont closes : un
  /// mot qu'elles ignorent — ou leur absence — n'est pas une saisie humaine mais un formulaire
  /// forgé. Il est refusé <b>en le nommant</b> plutôt qu'ignoré, faute de quoi le service
  /// enregistrerait autre chose que ce qu'on croit lui avoir dit.
  /// </para>
  /// <para>
  /// La lecture est passée en paramètre plutôt que contrainte par un type : <c>SmartEnum</c> expose
  /// son <c>TryFromName</c> en statique, que l'inférence générique ne sait pas rejoindre.
  /// </para>
  /// </remarks>
  /// <param name="modelState">L'endroit où les refus se déposent.</param>
  /// <param name="prefix">Le préfixe de liaison du formulaire, tel que la page l'a déclaré.</param>
  /// <param name="field">Le champ que le refus doit nommer.</param>
  /// <param name="name">Le mot envoyé par le formulaire.</param>
  /// <param name="read">La lecture du vocabulaire fermé.</param>
  /// <param name="complaint">
  /// Ce que le vocabulaire n'est pas, au singulier et sans point final — « n'est pas un droit de la
  /// taxonomie ». Il clôt un message lu par un humain.
  /// </param>
  internal static T? ReadVocabulary<T>(
    ModelStateDictionary modelState,
    string prefix,
    string field,
    string? name,
    Reading<T> read,
    string complaint)
    where T : class
  {
    ArgumentNullException.ThrowIfNull(modelState);
    ArgumentNullException.ThrowIfNull(read);

    if (read(name ?? string.Empty, out var value))
    {
      return value;
    }

    modelState.AddModelError($"{prefix}.{field}", $"« {name} » {complaint}.");

    return null;
  }

  /// <summary>
  /// Ce qu'un type du domaine rend, ou son refus déposé sous le nom du champ fautif — <b>le type
  /// lève, la frontière nomme</b>.
  /// </summary>
  /// <remarks>
  /// <b>Elle attrape l'<c>ArgumentException</c></b>, quand <see cref="Read{T}"/> attrape le refus de
  /// Vogen : les types écrits à la main de ce contexte lèvent la première, et les deux gestes
  /// vivent ici plutôt que d'être recopiés dans chaque écran.
  /// </remarks>
  /// <param name="modelState">L'endroit où les refus se déposent.</param>
  /// <param name="prefix">Le préfixe de liaison du formulaire, tel que la page l'a déclaré.</param>
  /// <param name="field">Le champ que le refus doit nommer.</param>
  /// <param name="cross">Le franchissement à tenter.</param>
  internal static T? Declared<T>(ModelStateDictionary modelState, string prefix, string field, Func<T> cross)
    where T : class
  {
    ArgumentNullException.ThrowIfNull(modelState);
    ArgumentNullException.ThrowIfNull(cross);

    try
    {
      return cross();
    }
    catch (ArgumentException refusal)
    {
      modelState.AddModelError($"{prefix}.{field}", Named(refusal));

      return null;
    }
  }

  /// <summary>
  /// Le message d'un type du domaine, <b>sans le nom du paramètre</b> qu'<c>ArgumentException</c> y
  /// accole : l'écran nomme déjà le champ fautif à côté de sa case, et le redire en anglais entre
  /// parenthèses ferait lire à l'humain un mot du C#.
  /// </summary>
  internal static string Named(ArgumentException refusal)
  {
    ArgumentNullException.ThrowIfNull(refusal);

    return refusal.Message.Split(" (Parameter")[0];
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

  /// <summary>La forme que <c>SmartEnum</c> donne à la lecture d'un vocabulaire fermé par son nom.</summary>
  internal delegate bool Reading<T>(string name, out T value);
}
