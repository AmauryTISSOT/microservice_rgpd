using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UseCases.Casework.ReadQueue;

/// <summary>
/// Relire la file, telle qu'elle se lit <b>à cet instant</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>La file est une requête, jamais un processus.</b> Rien ne tourne, donc rien ne peut s'arrêter
/// en silence : un processus de fond interrompu rendrait une file <b>vide et rassurante</b>, soit
/// l'<c>Omission silencieuse</c> sous sa forme la plus dangereuse, et ferait dépendre la preuve de ce
/// qu'un <c>cron</c> ait tourné.
/// </para>
/// <para>
/// <b>Aucun paramètre : ni page, ni filtre, ni tri au choix.</b> Un filtre est une façon de ne plus
/// voir, et un tri au choix ferait de l'ordre des échéances une préférence d'écran.
/// </para>
/// <para>
/// <b>Elle n'émet aucun appel.</b> La relance d'un <c>202</c> a lieu à l'ouverture d'un dossier ;
/// afficher une liste n'appelle pas un <c>Adapter</c> par ligne.
/// </para>
/// </remarks>
public sealed record ReadQueueQuery : IQuery<OperatorQueue>;

/// <summary>
/// La file de l'<c>Operator</c> : les dossiers ouverts, rangés par échéance, et l'instant où on les
/// a regardés.
/// </summary>
/// <remarks>
/// <b>Aucun total.</b> Ni « 4 dossiers ouverts », ni « 2 en retard » : la règle des chiffres
/// n'autorise le dénombrement que d'une chose présente que le service détient, et le seul intérêt
/// d'un total serait de se rassurer sans lire les lignes. <c>0 dossier en retard</c> doit être
/// impossible à produire — il l'est ici parce qu'aucun compte n'existe.
/// </remarks>
/// <param name="Cases">Les dossiers ouverts, dans l'ordre où il faut les prendre.</param>
/// <param name="ObservedAt">
/// L'instant sur lequel tous les dépassements de cette page ont été calculés. Il est <b>nommé</b> :
/// une file qui ne dirait pas de quand elle date se lirait comme une vérité intemporelle.
/// </param>
public sealed record OperatorQueue(IReadOnlyList<QueuedCase> Cases, DateTimeOffset ObservedAt);

/// <summary>
/// Une ligne de la file : un dossier ouvert, et ce qui sert à décider s'il passe avant un autre.
/// </summary>
/// <remarks>
/// <para>
/// <b>Les échéances sont des colonnes sur une ligne déjà présente.</b> Le dossier est ouvert, il est
/// dans la file ; l'échéance le <b>trie</b>, elle ne le fait pas apparaître. Aucune ligne d'ici ne
/// dépend de ce qu'un processus ait tourné.
/// </para>
/// <para>
/// <b>Aucun nombre de jours.</b> Une date et un dépassement sont des faits ; « en retard de 4 jours »
/// serait un compteur, et trier sur un seuil sans force juridique ferait passer un dossier devant un
/// autre sans raison juridique.
/// </para>
/// </remarks>
/// <param name="Case">L'identité du dossier — la seule façon de le désigner, la personne n'entrant pas ici.</param>
/// <param name="IdentityDeclaration">Ce que le canal d'entrée a déclaré de l'identité du demandeur.</param>
/// <param name="Reception">La date de réception, et si le service l'a tenue pour défaut.</param>
/// <param name="Deadline">L'échéance du mois de l'art. 12.3, calculée à l'instant de l'affichage.</param>
/// <param name="DelayOverrun">Le délai est-il dépassé à cet instant ? Un calcul, jamais un état.</param>
/// <param name="ClaimedRights">
/// Les droits réclamés, <b>énumérés et jamais comptés</b> : « Access, Erasure » dit à
/// l'<c>Operator</c> ce qu'il a à faire, là où « 2 droits » ne lui apprend rien.
/// </param>
/// <param name="RightsAwaitingAHandover">
/// Les droits dont quelqu'un a téléchargé la remise sans jamais déclarer l'avoir rendue —
/// <b>énumérés et jamais comptés</b>, comme les droits réclamés.
/// <para>
/// <b>C'est une colonne sur une ligne déjà présente</b>, jamais une file de plus. Le dossier est
/// ouvert, il est là ; ce que cette colonne ajoute est qu'un travail s'est arrêté au milieu du gué,
/// et le laisser invisible serait l'<c>Omission silencieuse</c> sous sa forme la plus tranquille :
/// la personne a une réponse assemblée que personne ne lui a rendue.
/// </para>
/// </param>
public sealed record QueuedCase(
  CaseId Case,
  IdentityDeclaration IdentityDeclaration,
  ReceptionDate Reception,
  StatutoryDeadline Deadline,
  bool DelayOverrun,
  IReadOnlyList<DataSubjectRight> ClaimedRights,
  IReadOnlyList<DataSubjectRight> RightsAwaitingAHandover);
