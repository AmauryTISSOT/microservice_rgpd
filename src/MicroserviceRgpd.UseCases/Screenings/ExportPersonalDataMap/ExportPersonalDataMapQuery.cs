using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.ExportPersonalDataMap;

/// <summary>
/// Rendre la <c>Cartographie</c> du rapport de détection courant, pour l'<c>Operator</c> qui a
/// cliqué.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>C'est la seule réponse de ce contexte qui ne soit pas un
/// <see cref="ScreeningAnswer{T}"/>.</b> La <see cref="PersonalDataMap"/> ne porte
/// <b>aucune</b> <see cref="IncompletenessClause"/>, sous aucune forme, et le renversement est
/// assumé : l'<c>Operator</c> a tranché chaque ligne et choisit d'expédier le fichier ; ce qu'il en
/// dit au destinataire lui appartient. Voir <see cref="PersonalDataMap"/>, où le motif et les cinq
/// mécanismes de rattrapage écartés sont écrits une fois.
/// </para>
/// <para>
/// ⚠️ <b>L'exception est <i>nommée</i> dans le garde, jamais silencieuse.</b>
/// <c>IncompletenessClauseTravelsWithEveryAnswerTests</c> connaît ce handler par son nom et par son
/// motif : toute <i>autre</i> réponse rendant un rapport sans sa clause rougit, et celle-ci se lit
/// comme une décision plutôt que comme un oubli.
/// </para>
/// <para>
/// <b>Elle rend <c>null</c> quand le déploiement n'a lancé aucune détection</b> — comme
/// <c>ReadCurrentScreeningQuery</c>, et pour la même raison : une cartographie vide serait un
/// document qui affirme n'avoir rien trouvé là où personne n'a rien cherché.
/// </para>
/// </remarks>
public sealed record ExportPersonalDataMapQuery : IQuery<PersonalDataMap?>;
