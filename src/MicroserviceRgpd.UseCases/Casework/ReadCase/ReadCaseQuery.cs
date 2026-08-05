using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UseCases.Casework.ReadCase;

/// <summary>
/// Relire un dossier, entier. C'est le <b>seul endroit où l'<c>Operator</c> agit</b>, et donc le seul
/// écran qui doive tout montrer d'un coup.
/// </summary>
/// <param name="Case">L'identité du dossier à relire.</param>
public sealed record ReadCaseQuery(CaseId Case) : IQuery<CaseOnScreen?>;

/// <summary>
/// Le dossier tel que l'écran le montre : son bandeau, ses réclamations, et le travail dû sous
/// chacune.
/// </summary>
/// <remarks>
/// <b>Aucun total, aucun taux, aucun dénominateur.</b> Ni « 2 systèmes traités sur 6 », ni « 33 % » :
/// un dénominateur qui décrit le paysage du client donnerait à une déclaration qui vieillit exprès
/// l'autorité d'un recensement. L'écran <b>énumère</b>.
/// </remarks>
/// <param name="Case">L'identité du dossier.</param>
/// <param name="IdentityDeclaration">Ce que le canal d'entrée a déclaré de l'identité du demandeur.</param>
/// <param name="Reception">La date de réception, et si le service l'a tenue pour défaut.</param>
/// <param name="Deadline">L'échéance de l'art. 12.3, calculée à l'instant de l'affichage.</param>
/// <param name="DelayOverrun">Le délai est-il dépassé à cet instant ? Un calcul, jamais un état.</param>
/// <param name="OldestStepDeclaration">
/// La plus ancienne date de déclaration parmi les systèmes dont ce dossier porte le travail dû, ou
/// <c>null</c> si le catalogue était vide à l'ouverture. <b>C'est elle que le bandeau nomme</b> : la
/// fraîcheur d'un recensement est celle de sa ligne la plus vieille, et c'est au moment où quelqu'un
/// signe qu'une déclaration vieille doit lui être rappelée.
/// </param>
/// <param name="Claims">Les droits réclamés, et sous chacun le travail dû.</param>
/// <param name="ObservedAt">L'instant sur lequel le dépassement a été calculé.</param>
public sealed record CaseOnScreen(
  CaseId Case,
  IdentityDeclaration IdentityDeclaration,
  ReceptionDate Reception,
  StatutoryDeadline Deadline,
  bool DelayOverrun,
  DateTimeOffset? OldestStepDeclaration,
  IReadOnlyList<ClaimedRight> Claims,
  DateTimeOffset ObservedAt);

/// <summary>
/// Un droit réclamé, et le travail dû qu'il porte. <b>Éventuellement aucun</b> : c'est l'état d'un
/// service dont le <c>Manifest</c> n'était pas déclaré quand le dossier s'est ouvert, et l'écran doit
/// pouvoir le dire.
/// </summary>
/// <param name="Right">Le droit réclamé.</param>
/// <param name="State">Où en est la réponse du service sur ce droit.</param>
/// <param name="Steps">Le travail dû, un par système déclaré au moment de l'ouverture.</param>
public sealed record ClaimedRight(
  DataSubjectRight Right,
  ClaimState State,
  IReadOnlyList<StepOnScreen> Steps);

/// <summary>
/// Le travail dû sur un système, tel que l'écran le montre — <b>y compris ce que le service ne sait
/// plus dire de ce système</b>.
/// </summary>
/// <param name="DeclaredSystem">L'identifiant du système, celui que l'<c>Adapter</c> reçoit.</param>
/// <param name="Label">
/// Le nom sous lequel l'<c>Operator</c> reconnaît ce système, ou <c>null</c> si le catalogue ne le
/// porte plus. Le <c>null</c> se dit à l'écran plutôt que de faire disparaître la ligne : un travail
/// dû dont le système a quitté le catalogue reste dû.
/// </param>
/// <param name="DeclaredOn">
/// Le jour où un humain a déclaré ce système, ou <c>null</c> s'il ne figure plus au catalogue.
/// <b>La date du <c>Manifest</c> descend ici</b> : c'est au moment où quelqu'un signe qu'une
/// déclaration vieille se lit.
/// </param>
/// <param name="State">L'état déclaré de ce travail. Cinq valeurs, dont deux qui refusent de fusionner.</param>
/// <param name="AwaitsAFinding">
/// L'écran doit-il <b>réclamer un constat</b> sur ce travail dû ? Vrai d'un <c>Done</c> pour lequel le
/// service ne détient <b>aucun rattachement</b> : six zéros ne doivent pas se lire « cette personne
/// n'est pas chez nous ».
/// <para>
/// ⚠️ <b>C'est une lecture de <see cref="StepState.RequiresAFinding"/>, et surtout pas un état
/// nouveau.</b> Un sixième état de <c>Step</c> — « fait, mais à constater » — aurait fait porter au
/// dossier une exigence de la surface, et il aurait fallu le faire retomber quelque part.
/// </para>
/// <para>
/// <b>La même règle refuse la déclaration</b> qu'aucun constat n'accompagne : ce que l'écran réclame
/// ici, la ligne de preuve l'exige — sinon la demande ne serait qu'un paragraphe qu'on peut ignorer.
/// </para>
/// </param>
public sealed record StepOnScreen(
  DeclaredSystemId DeclaredSystem,
  SystemLabel? Label,
  DateTimeOffset? DeclaredOn,
  StepState State,
  bool AwaitsAFinding);
