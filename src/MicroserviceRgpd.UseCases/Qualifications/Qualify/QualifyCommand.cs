using MicroserviceRgpd.Core.Qualifications;

namespace MicroserviceRgpd.UseCases.Qualifications.Qualify;

/// <summary>
/// Qualifier un texte reçu d'une application tierce, et rendre le verdict dans le même échange.
/// </summary>
/// <param name="Text">
/// Le texte à qualifier. Il arrive <b>déjà validé par son type</b> : la frontière d'entrée a
/// refusé l'absent, le vide et le démesuré, de sorte qu'aucun texte inqualifiable n'atteint le
/// moteur.
/// </param>
/// <param name="CallerReference">
/// La référence de l'appelant, <b>opaque</b>. Le service ne l'interprète jamais, ne la normalise
/// pas, n'en impose pas l'unicité, et la traverse jusqu'à la réponse telle qu'elle est arrivée.
/// <para>
/// <b>Ce n'est pas une clé d'idempotence</b> : deux appels qui la partagent produisent deux
/// qualifications distinctes et deux identifiants distincts.
/// </para>
/// </param>
public sealed record QualifyCommand(RightsRequestText Text, string? CallerReference)
  : ICommand<Result<QualificationOutcome>>
{
  /// <summary>
  /// Le plafond de la référence appelante, en unités UTF-16.
  /// </summary>
  /// <remarks>
  /// Chiffre arbitraire et assumé. Il vit ici, et non à la frontière d'entrée qui l'applique,
  /// parce que la trace d'audit devra le refléter : deux constantes valant chacune 64 finiraient
  /// par ne plus valoir la même chose, et la base cesserait alors d'accepter ce que le contrat
  /// promet — ou l'inverse.
  /// </remarks>
  public const int MaxCallerReferenceLength = 64;
}
