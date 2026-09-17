using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UseCases.Configuration.ClearRightChannel;

/// <summary>
/// Ramener un droit à « non configuré » : oublier le canal par lequel le service l'exercerait.
/// </summary>
/// <remarks>
/// <b>Un seul droit par commande.</b> Il n'existe pas de « tout effacer » : chaque droit se
/// configure — et se déconfigure — isolément.
/// <para>
/// <b>Une seule commande pour les deux canaux</b> (ADR-0027, écart n° 1). Le canal étant un type
/// fermé, effacer un routage et effacer une adresse sont le même geste : ramener un droit à « non
/// configuré ». Deux commandes identiques au caractère près seraient une divergence en attente.
/// </para>
/// </remarks>
/// <param name="Right">Le droit à effacer — l'un des six du périmètre, jamais <c>OutOfScope</c>.</param>
public sealed record ClearRightChannelCommand(DataSubjectRight Right) : ICommand<Result>;
