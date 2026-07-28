using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.UseCases.Qualifications.Qualify;

namespace MicroserviceRgpd.Web.Qualifications;

/// <summary>
/// La qualification rendue à l'application tierce.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ce que la réponse tait délibérément</b> : le texte en écho, tout horodatage, les avis bruts
/// de chaque moteur, la confiance qu'un moteur déclare, et l'identité des moteurs. Ces trois
/// dernières circulent à l'intérieur du service sans franchir cette frontière — les publier
/// graverait l'architecture dans le contrat public et inviterait l'appelant à recalculer, hors de
/// tout test, la règle que le service tient pour lui.
/// </para>
/// <para>
/// <b>Règle d'évolution</b> : ajouter un champ facultatif est rétro-compatible ; en retirer un,
/// en renommer un, durcir une contrainte d'entrée ou ajouter une valeur à la taxonomie ou au
/// signal de relecture sont des <b>ruptures</b>.
/// </para>
/// </remarks>
/// <param name="QualificationId">
/// L'identité que le service donne à cette qualification. <b>Toujours présente</b>, durable, et
/// c'est elle — jamais un identifiant de télémétrie, dont la rétention et l'échantillonnage
/// échappent au service — qu'un ticket de support citera.
/// </param>
/// <param name="CallerReference">La référence fournie par l'appelant, rendue verbatim. Absente s'il n'en a pas fourni.</param>
/// <param name="Rights">
/// Les droits reconnus, sous leurs noms canoniques. <b>Jamais vide</b> — « aucun droit reconnu »
/// s'écrit <c>OutOfScope</c>, qui est un verdict — et <c>OutOfScope</c> n'y apparaît jamais
/// accompagné d'un autre droit.
/// </param>
/// <param name="ReviewSignal">
/// L'urgence à relire. <b>Toujours présent</b> : un signal de tri facultatif ne trie plus, et
/// obligerait chaque appelant à traiter une absence dont le traitement prudent serait de toute
/// façon « à relire ».
/// </param>
/// <param name="Degraded">
/// Vrai quand le service n'était pas entier au moment de rendre ce verdict. <b>Toujours présent</b>,
/// et distinct du signal de relecture : celui-ci dit « avec quelle attention relire ? », celui-là
/// dit « le service était-il entier ? ». Les fondre ferait perdre l'un pour lire l'autre.
/// </param>
public sealed record QualifyResponse(
  Guid QualificationId,
  string? CallerReference,
  IReadOnlyCollection<DataSubjectRight> Rights,
  ReviewSignal ReviewSignal,
  bool Degraded)
{
  /// <summary>
  /// Projette sur le fil ce que le service a rendu.
  /// </summary>
  /// <remarks>
  /// Les droits sortent dans l'ordre de la taxonomie. Le domaine les tient pour un <b>ensemble</b>,
  /// sans ordre signifiant ; en figer un ici évite que la réponse varie d'un appel à l'autre sans
  /// que rien n'ait changé — et personne n'a besoin d'un ordre instable pour se convaincre qu'un
  /// ensemble n'en a pas.
  /// </remarks>
  public static QualifyResponse From(QualificationOutcome outcome)
  {
    ArgumentNullException.ThrowIfNull(outcome);

    return new QualifyResponse(
      outcome.QualificationId,
      outcome.CallerReference,
      [.. outcome.Qualification.Rights.OrderBy(right => right.Value)],
      outcome.ReviewSignal,
      outcome.Degraded);
  }
}
