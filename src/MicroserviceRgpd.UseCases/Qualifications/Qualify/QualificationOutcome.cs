using MicroserviceRgpd.Core.Qualifications;

namespace MicroserviceRgpd.UseCases.Qualifications.Qualify;

/// <summary>
/// Ce que le service a rendu pour un texte : le verdict, l'identité durable qu'il lui a donnée, les
/// <b>deux axes</b> par lesquels l'appelant le lit, et les <b>prémisses</b> dont le verdict est tiré.
/// </summary>
/// <remarks>
/// <para>
/// Les deux axes sont distincts et le restent : <see cref="ReviewSignal"/> répond à « avec quelle
/// attention dois-je relire ce verdict ? », <see cref="Degraded"/> à « le service était-il entier
/// quand il l'a produit ? ». Les fondre forcerait l'appelant à perdre l'un pour lire l'autre.
/// </para>
/// <para>
/// <b>Les deux avis et les trois latences remontent ici</b>, alors même que le contrat public les
/// refuse à l'application tierce. La logique s'inverse parce que ce type est interne : ils sont ce
/// qui explique le verdict et le signal de relecture, et l'écran qui les déplie n'a pas à ouvrir un
/// second chemin de qualification pour les obtenir. Leur non-publication reste tenue à la frontière,
/// par <c>QualifyResponse</c>, qui ne projette rien de tout cela.
/// </para>
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
/// en repli sur le lexique : celui-ci ne justifie rien, et lui fabriquer une phrase mentirait à
/// l'opérateur au moment précis où le service se trompe le plus.
/// </param>
/// <param name="CallerReference">La référence de l'appelant, rendue verbatim, ou absente si elle ne fut pas fournie.</param>
/// <param name="VerdictOpinion">
/// L'avis du moteur qui fait verdict, tel qu'il est arrivé — absent quand ce moteur n'a rien rendu,
/// et c'est alors le repli sur le lexique que porte ce résultat.
/// </param>
/// <param name="LexiconOpinion">
/// L'avis du moteur lexical, tel qu'il est arrivé — absent quand ce moteur n'a rien rendu, et c'est
/// alors un verdict resté sans contrôle que porte ce résultat.
/// </param>
/// <param name="TotalLatency">Le temps qu'a pris la qualification entière, hors écriture de la trace.</param>
/// <param name="VerdictLatency">
/// Le temps qu'a pris le moteur de verdict, ou rien s'il n'a pas rendu d'avis : mesurer le temps
/// qu'il a mis à ne rien rendre ferait passer une panne pour une lenteur.
/// </param>
/// <param name="LexiconLatency">
/// Le temps qu'a pris le moteur lexical, ou rien s'il n'a pas rendu d'avis, et pour la même raison.
/// </param>
public sealed record QualificationOutcome(
  Guid QualificationId,
  Qualification Qualification,
  ReviewSignal ReviewSignal,
  bool Degraded,
  string? Justification,
  string? CallerReference,
  QualificationOpinion? VerdictOpinion,
  QualificationOpinion? LexiconOpinion,
  TimeSpan TotalLatency,
  TimeSpan? VerdictLatency,
  TimeSpan? LexiconLatency);
