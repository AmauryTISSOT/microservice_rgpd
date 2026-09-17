using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.SharedKernel;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MicroserviceRgpd.Web.Pages.Configuration;

/// <summary>
/// Ce que l'intégrateur saisit dans la mini-form d'<b>un</b> droit sur la face RabbitMQ — <b>deux
/// chaînes</b>, jusqu'à ce qu'elles franchissent la frontière du domaine.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un droit par envoi</b>, comme toute mini-form du Paramétrage : voir <see cref="RightForm"/>.
/// </para>
/// <para>
/// ⚠️ <b>Les deux champs sont jugés tous les deux</b>, et non l'un après l'autre : une saisie où
/// l'exchange et la routing key sont fautifs se lit d'un coup, chaque refus rangé sous son champ.
/// Refuser au premier venu aurait fait corriger en deux allers-retours.
/// </para>
/// <para>
/// ⚠️ <b>Rien ici n'appelle le broker</b> : construire un routage ne s'y connecte pas, n'y déclare
/// aucun exchange et ne vérifie rien (ADR-0027). Ce que l'écran écrit est une déclaration.
/// </para>
/// </remarks>
public sealed class RightRabbitMqForm : RightForm
{
  /// <summary>L'exchange saisi pour ce droit.</summary>
  public string? Exchange { get; set; }

  /// <summary>La routing key saisie pour ce droit.</summary>
  public string? RoutingKey { get; set; }

  /// <summary>
  /// Fait franchir la saisie à la frontière du domaine, ou <b>nomme à l'intégrateur</b> ce qui a été
  /// refusé. Les messages viennent des types du domaine eux-mêmes, jamais d'une seconde rédaction.
  /// </summary>
  /// <param name="modelState">L'endroit où les refus se déposent, sous le nom du champ fautif.</param>
  /// <param name="prefix">Le préfixe de liaison du formulaire, tel que la page l'a déclaré.</param>
  /// <returns>Le droit et son routage, ou <c>null</c> si l'un des trois a été refusé.</returns>
  public (DataSubjectRight Right, RabbitMqRouting Routing)? Read(ModelStateDictionary modelState, string prefix)
  {
    ArgumentNullException.ThrowIfNull(modelState);

    var right = ReadRight(modelState, prefix);

    // Un champ laissé vide arrive `null` de la liaison : il entre comme vide, pour que ce soit la
    // règle du type — écrite en français — qui parle à qui a saisi.
    var exchange = Crossed<ExchangeName>(
      () => ExchangeName.From(Exchange ?? string.Empty), modelState, $"{prefix}.{nameof(Exchange)}");

    var routingKey = Crossed<Core.Configuration.RoutingKey>(
      () => Core.Configuration.RoutingKey.From(RoutingKey ?? string.Empty),
      modelState,
      $"{prefix}.{nameof(RoutingKey)}");

    if (right is null || exchange is null || routingKey is null)
    {
      return null;
    }

    return (right, new RabbitMqRouting(exchange.Value, routingKey.Value));
  }
}
