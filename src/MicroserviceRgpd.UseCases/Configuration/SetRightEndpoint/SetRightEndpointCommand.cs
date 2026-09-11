using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UseCases.Configuration.SetRightEndpoint;

/// <summary>
/// Poser l'adresse à laquelle le service exercera un droit — la créer ou la remplacer.
/// </summary>
/// <remarks>
/// <b>Un seul droit par commande.</b> Il n'existe pas de « tout enregistrer » : chaque droit se
/// configure isolément, et une commande qui en porterait six ferait d'une correction une réécriture
/// de toute la configuration.
/// </remarks>
/// <param name="Right">Le droit à configurer — l'un des six du périmètre, jamais <c>OutOfScope</c>.</param>
/// <param name="Endpoint">L'adresse d'exercice, valide par construction.</param>
public sealed record SetRightEndpointCommand(DataSubjectRight Right, EndpointUrl Endpoint)
  : ICommand<Result>;
