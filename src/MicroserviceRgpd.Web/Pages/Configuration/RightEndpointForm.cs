using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.SharedKernel;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MicroserviceRgpd.Web.Pages.Configuration;

/// <summary>
/// Ce que l'intégrateur saisit dans la mini-form d'<b>un</b> droit sur la face HTTP — <b>une
/// chaîne</b>, jusqu'à ce qu'elle franchisse la frontière du domaine.
/// </summary>
/// <remarks>
/// <b>Un droit par envoi</b>, comme toute mini-form du Paramétrage : voir <see cref="RightForm"/>.
/// </remarks>
public sealed class RightEndpointForm : RightForm
{
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
    var endpoint = Crossed(
      () => EndpointUrl.From(Url ?? string.Empty), modelState, $"{prefix}.{nameof(Url)}");

    if (right is null || endpoint is null)
    {
      return null;
    }

    return (right, endpoint.Value);
  }
}
