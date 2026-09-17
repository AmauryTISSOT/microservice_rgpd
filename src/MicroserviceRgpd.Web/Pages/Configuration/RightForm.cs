using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.SharedKernel;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Vogen;

namespace MicroserviceRgpd.Web.Pages.Configuration;

/// <summary>
/// Ce que <b>toute</b> mini-form du Paramétrage porte : le <b>droit</b> qu'elle configure, et lui
/// seul. Les deux faces en héritent — l'une y ajoute une adresse, l'autre un exchange et une routing
/// key.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un droit par envoi.</b> Chaque droit porte sa propre mini-form, et l'envoi ne transporte que
/// lui : il n'existe aucun champ par lequel un enregistrement toucherait le canal d'un autre droit.
/// </para>
/// <para>
/// <b>Des chaînes, et rien que des chaînes</b>, jusqu'à ce qu'elles franchissent la frontière du
/// domaine : c'est là que la règle du type — écrite en français — parle à qui a saisi.
/// </para>
/// </remarks>
public abstract class RightForm
{
  /// <summary>Le droit de la mini-form, par son nom canonique anglais — un champ caché.</summary>
  public string? Right { get; set; }

  /// <summary>
  /// Le droit que cette mini-form désigne, s'il est l'un des six que le Paramétrage configure —
  /// <c>null</c> pour un nom forgé, <c>OutOfScope</c> compris.
  /// </summary>
  public DataSubjectRight? Designated =>
    Settings.ConfigurableRights.FirstOrDefault(candidate => candidate.Name == Right);

  /// <summary>
  /// Fait franchir <b>le seul droit</b> à la frontière du domaine — ce qu'un effacement envoie, sans
  /// canal —, ou nomme à l'intégrateur le droit refusé.
  /// </summary>
  /// <param name="modelState">L'endroit où le refus se dépose, sous le nom du champ du droit.</param>
  /// <param name="prefix">Le préfixe de liaison du formulaire, tel que la page l'a déclaré.</param>
  /// <returns>Le droit désigné, ou <c>null</c> s'il a été refusé.</returns>
  public DataSubjectRight? ReadRight(ModelStateDictionary modelState, string prefix)
  {
    ArgumentNullException.ThrowIfNull(modelState);

    // Les droits de l'écran sont clos — les six du périmètre, OutOfScope exclu : un nom qu'ils
    // ignorent n'est pas une saisie humaine mais un formulaire forgé, refusé en le nommant.
    var right = Designated;

    if (right is null)
    {
      modelState.AddModelError($"{prefix}.{nameof(Right)}", $"« {Right} » n'est pas un droit du Paramétrage.");
    }

    return right;
  }

  /// <summary>
  /// Fait franchir <b>un</b> champ à la frontière du domaine, ou dépose son refus sous le nom de ce
  /// champ. Le geste est écrit une fois : tous les champs de toutes les faces sont jugés de la même
  /// façon, et <b>chacun l'est même si un autre a déjà été refusé</b> — une saisie doublement fautive
  /// se lit d'un coup, plutôt qu'en deux allers-retours.
  /// </summary>
  /// <typeparam name="T">Le type du domaine qui porte la règle, et son message.</typeparam>
  /// <param name="crossing">La construction du type, qui refuse en levant.</param>
  /// <param name="modelState">L'endroit où le refus se dépose.</param>
  /// <param name="field">Le nom du champ fautif, préfixe compris.</param>
  /// <returns>La valeur franchie, ou <c>null</c> si elle a été refusée.</returns>
  protected static T? Crossed<T>(Func<T> crossing, ModelStateDictionary modelState, string field)
    where T : struct
  {
    ArgumentNullException.ThrowIfNull(crossing);
    ArgumentNullException.ThrowIfNull(modelState);

    try
    {
      return crossing();
    }
    catch (ValueObjectValidationException refusal)
    {
      modelState.AddModelError(field, refusal.Message);

      return null;
    }
  }
}
