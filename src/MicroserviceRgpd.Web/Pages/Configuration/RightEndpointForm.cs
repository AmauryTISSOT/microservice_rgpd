using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.SharedKernel;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Vogen;

namespace MicroserviceRgpd.Web.Pages.Configuration;

/// <summary>
/// Ce que l'intégrateur saisit dans la mini-form d'<b>un</b> droit — <b>des chaînes, et rien que des
/// chaînes</b>, jusqu'à ce qu'elles franchissent la frontière du domaine.
/// </summary>
/// <remarks>
/// <b>Un droit par envoi.</b> Chaque droit porte sa propre mini-form, et l'envoi ne transporte que
/// lui : il n'existe aucun champ par lequel un enregistrement toucherait l'adresse d'un autre droit.
/// </remarks>
public sealed class RightEndpointForm
{
  /// <summary>Le droit de la mini-form, par son nom canonique anglais — un champ caché.</summary>
  public string? Right { get; set; }

  /// <summary>L'adresse saisie pour ce droit.</summary>
  public string? Url { get; set; }

  /// <summary>
  /// Fait franchir la saisie à la frontière du domaine, ou <b>nomme à l'intégrateur</b> ce qui a été
  /// refusé. Les messages viennent des types du domaine eux-mêmes, jamais d'une seconde rédaction.
  /// </summary>
  /// <param name="modelState">L'endroit où les refus se déposent, sous le nom du champ fautif.</param>
  /// <param name="prefix">Le préfixe de liaison du formulaire, tel que la page l'a déclaré.</param>
  /// <returns>Le droit et son adresse, ou <c>null</c> si l'un des deux a été refusé.</returns>
  public (DataSubjectRight Right, EndpointUrl Endpoint)? Read(ModelStateDictionary modelState, string prefix)
  {
    ArgumentNullException.ThrowIfNull(modelState);

    var right = ReadRight(modelState, prefix);

    // Un champ laissé vide arrive `null` de la liaison : il entre comme vide, pour que ce soit la
    // règle du type — écrite en français — qui parle à qui a saisi.
    EndpointUrl? endpoint = null;

    try
    {
      endpoint = EndpointUrl.From(Url ?? string.Empty);
    }
    catch (ValueObjectValidationException refusal)
    {
      modelState.AddModelError($"{prefix}.{nameof(Url)}", refusal.Message);
    }

    if (right is null || endpoint is null)
    {
      return null;
    }

    return (right, endpoint.Value);
  }

  /// <summary>
  /// Fait franchir <b>le seul droit</b> à la frontière du domaine — ce qu'un effacement envoie, sans
  /// adresse —, ou nomme à l'intégrateur le droit refusé.
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
  /// Le droit que cette mini-form désigne, s'il est l'un des six que le Paramétrage configure —
  /// <c>null</c> pour un nom forgé, <c>OutOfScope</c> compris.
  /// </summary>
  public DataSubjectRight? Designated =>
    Settings.ConfigurableRights.FirstOrDefault(candidate => candidate.Name == Right);
}
