using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UseCases.Configuration.SetRightRabbitMqRouting;

/// <summary>
/// Poser le routage RabbitMQ par lequel le service exercera un droit — le créer ou le remplacer.
/// </summary>
/// <remarks>
/// <b>Un seul droit par commande</b>, en miroir de la commande qui pose une adresse : chaque droit se
/// configure isolément, et une commande qui en porterait six ferait d'une correction une réécriture
/// de toute la configuration.
/// <para>
/// ⚠️ <b>Il n'y a pas de commande d'effacement jumelle</b> : effacer un routage et effacer une adresse
/// sont le même geste, et une seule commande le porte (ADR-0027, écart n° 1).
/// </para>
/// </remarks>
/// <param name="Right">Le droit à configurer — l'un des six du périmètre, jamais <c>OutOfScope</c>.</param>
/// <param name="Routing">L'exchange et la routing key, valides par construction.</param>
public sealed record SetRightRabbitMqRoutingCommand(DataSubjectRight Right, RabbitMqRouting Routing)
  : ICommand<Result>;
