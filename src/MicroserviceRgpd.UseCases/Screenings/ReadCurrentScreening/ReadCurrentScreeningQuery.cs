using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.ReadCurrentScreening;

/// <summary>
/// Relire le <b>rapport sommaire</b> du dépistage courant.
/// </summary>
/// <remarks>
/// <para>
/// <b>« Courant » est un calcul</b> : le rapport le plus récemment lancé du déploiement. Les autres
/// sont archivés par le seul fait que celui-ci existe, et rien n'a été écrit pour cela.
/// </para>
/// <para>
/// ⚠️ <b>Elle rend <c>null</c> quand le déploiement n'a lancé aucun dépistage</b>, et l'écran rend
/// alors le dépôt seul. Un rapport vide portant « ce dépistage n'a pas regardé le CRM en SaaS, les
/// tableurs partagés, les journaux… » serait un <b>aveu sans acte</b>, et userait la
/// <c>Clause d'incomplétude</c> avant son premier usage réel. ⚠️ Ce n'est pas le cas spécial « quand
/// rien n'est signalé », qui reste refusé : là il y a un dépistage réel dont on tairait le seuil
/// zéro, ici il n'y a pas de dépistage du tout.
/// </para>
/// <para>
/// <b>Elle rend un <see cref="ScreeningAnswer{T}"/>, et c'est une obligation.</b> Toute réponse
/// rendant un <c>Screening</c> ou une <c>ScreenedColumn</c> porte la
/// <c>Clause d'incomplétude</c> — structurée plutôt qu'en prose pour la seule raison qu'elle soit
/// <b>testable</b>, et posée sur la réponse plutôt qu'en pied de page pour qu'elle ne se replie pas.
/// </para>
/// </remarks>
public sealed record ReadCurrentScreeningQuery : IQuery<ScreeningAnswer<ScreeningSummary>?>;
