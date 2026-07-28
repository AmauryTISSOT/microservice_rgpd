using MicroserviceRgpd.Core.Qualifications;

namespace MicroserviceRgpd.UseCases.Qualifications.Qualify;

/// <summary>
/// Ce que le service a rendu pour un texte : le verdict, l'identité durable qu'il lui a donnée, et
/// les <b>deux axes</b> par lesquels l'appelant le lit.
/// </summary>
/// <remarks>
/// Les deux axes sont distincts et le restent : <see cref="ReviewSignal"/> répond à « avec quelle
/// attention dois-je relire ce verdict ? », <see cref="Degraded"/> à « le service était-il entier
/// quand il l'a produit ? ». Les fondre forcerait l'appelant à perdre l'un pour lire l'autre.
/// </remarks>
/// <param name="QualificationId">
/// L'identité que le service donne à cette qualification, forgée par lui et <b>toujours présente</b>.
/// Elle deviendra la clé primaire de la trace d'audit : c'est par elle, et par aucun identifiant de
/// télémétrie, qu'on répondra plus tard d'un verdict.
/// </param>
/// <param name="Qualification">Le verdict, portant déjà ses deux invariants.</param>
/// <param name="ReviewSignal">L'urgence à relire. Toujours présent : un signal de tri facultatif ne trie plus.</param>
/// <param name="Degraded">
/// Vrai quand le service n'était pas entier — un moteur n'ayant pas rendu d'avis. Toujours présent :
/// un booléen facultatif obligerait chaque appelant à traiter trois états là où le domaine en a deux.
/// </param>
/// <param name="Justification">
/// La phrase que le moteur principal oppose à l'opérateur humain, quand il en a rendu une. Absente
/// en repli sur le témoin : celui-ci ne justifie rien, et lui fabriquer une phrase mentirait à
/// l'opérateur au moment précis où le service se trompe le plus.
/// </param>
/// <param name="CallerReference">La référence de l'appelant, rendue verbatim, ou absente si elle ne fut pas fournie.</param>
public sealed record QualificationOutcome(
  Guid QualificationId,
  Qualification Qualification,
  ReviewSignal ReviewSignal,
  bool Degraded,
  string? Justification,
  string? CallerReference);
