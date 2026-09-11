using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UseCases.Configuration.ClearRightEndpoint;

/// <summary>
/// Ramener un droit à « non configuré » : oublier l'adresse à laquelle le service l'exercerait.
/// </summary>
/// <remarks>
/// <b>Un seul droit par commande.</b> Il n'existe pas de « tout effacer » : chaque droit se
/// configure — et se déconfigure — isolément.
/// </remarks>
/// <param name="Right">Le droit à effacer — l'un des six du périmètre, jamais <c>OutOfScope</c>.</param>
public sealed record ClearRightEndpointCommand(DataSubjectRight Right) : ICommand<Result>;
