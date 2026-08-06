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
/// <param name="Motivation">
/// Ce que l'humain a pesé avant d'ouvrir ces droits sous cette identité, ou <c>null</c> si personne
/// ne l'a pesé. La méthode se lit à l'écran comme au <c>Ledger</c> ; le détail ne se lit qu'ici, et
/// meurt avec le dossier.
/// </param>
/// <param name="AwaitsAMotivation">
/// L'écran doit-il <b>réclamer une motivation</b> que personne n'a écrite ? Vrai quand au moins un
/// droit l'exige et qu'aucune n'a été donnée.
/// <para>
/// ⚠️ <b>Elle ne barre rien.</b> Le dossier est ouvert, le délai court, et l'exigence non satisfaite
/// reste affichée tant qu'elle ne l'est pas : la faiblesse d'un dossier doit rester <b>visible</b>
/// plutôt que contournée. Un refus à l'entrée l'aurait fait disparaître — soit en renvoyant la
/// personne à son silence, soit en faisant cocher n'importe quoi.
/// </para>
/// </param>
/// <param name="Claims">Les droits réclamés, et sous chacun le travail dû.</param>
/// <param name="Locatings">
/// Ce que les <c>Locate</c> ont rapporté, <b>un par système qui déclare la capacité</b> — appelé ou
/// non. Un système non appelé y figure avec sa localisation à <c>null</c> : « pas appelé » et
/// « appelé, rien trouvé » sont deux déclarations différentes, et l'écran ne les confond pas.
/// </param>
/// <param name="Questions">
/// Les questions ouvertes du dossier, datées. ⚠️ <b>Aucune ancienneté n'est calculée</b> : on montre
/// la date à laquelle la question a été posée, jamais « sans réponse depuis N jours » — aucun nombre
/// du droit ne fonderait N.
/// </param>
/// <param name="ObservedAt">L'instant sur lequel le dépassement a été calculé.</param>
public sealed record CaseOnScreen(
  CaseId Case,
  IdentityDeclaration IdentityDeclaration,
  IdentityMotivation? Motivation,
  bool AwaitsAMotivation,
  ReceptionDate Reception,
  StatutoryDeadline Deadline,
  bool DelayOverrun,
  DateTimeOffset? OldestStepDeclaration,
  IReadOnlyList<ClaimedRight> Claims,
  IReadOnlyList<LocatingOnScreen> Locatings,
  IReadOnlyList<OpenQuestion> Questions,
  DateTimeOffset ObservedAt);

/// <summary>
/// Ce qu'un <c>Locate</c> a rapporté d'un système, tel que l'écran le montre — <b>y compris qu'il
/// n'a pas été appelé</b>.
/// </summary>
/// <remarks>
/// <b>Le domaine descend ici tel quel</b>, plutôt que recopié champ par champ. Une seconde forme du
/// noyau certain et des réserves n'aurait rien ajouté qu'une occasion de diverger : ce que l'écran
/// montre <b>est</b> ce que le dossier porte, et la prose de motif s'y lit telle qu'elle est arrivée.
/// </remarks>
/// <param name="DeclaredSystem">L'identifiant du système, celui que l'<c>Adapter</c> a reçu.</param>
/// <param name="Label">
/// Le nom sous lequel l'<c>Operator</c> reconnaît ce système, ou <c>null</c> si le catalogue ne le
/// porte plus.
/// </param>
/// <param name="Locating">
/// Ce que l'appel a rapporté, ou <c>null</c> si le service n'a pas encore obtenu de réponse — jamais
/// appelé, ou toutes ses tentatives tombées en panne.
/// </param>
public sealed record LocatingOnScreen(
  DeclaredSystemId DeclaredSystem,
  SystemLabel? Label,
  Locating? Locating);

/// <summary>
/// Un droit réclamé, et le travail dû qu'il porte. <b>Éventuellement aucun</b> : c'est l'état d'un
/// service dont le <c>Manifest</c> n'était pas déclaré quand le dossier s'est ouvert, et l'écran doit
/// pouvoir le dire.
/// </summary>
/// <param name="Right">Le droit réclamé.</param>
/// <param name="State">Où en est la réponse du service sur ce droit.</param>
/// <param name="Origin">D'où vient la reconnaissance de ce droit — figée à sa naissance.</param>
/// <param name="IdentityAtOrigin">
/// Ce que le canal déclarait de l'identité <b>quand ce droit s'est ouvert</b>, et non ce qu'il en
/// déclare aujourd'hui : une déclaration relevée plus tard ne réécrit pas la preuve d'hier.
/// </param>
/// <param name="AwaitsConfirmation">
/// Ce droit attend-il encore qu'un humain le reprenne à son compte ? Vrai d'un
/// <see cref="ClaimOrigin.Proposed"/> non confirmé.
/// <para>
/// ⚠️ <b>C'est une <c>OpenQuestion</c>, jamais un blocage</b> : elle est visible <b>dans</b> le
/// dossier ouvert, pendant que le délai court, et il n'existe aucun état d'attente hors du
/// <c>Case</c>.
/// </para>
/// </param>
/// <param name="MotivationIsDemanded">
/// Ce droit, sous l'identité de sa naissance, réclame-t-il une motivation ? Vrai d'un
/// <c>Access</c> ouvert sous une déclaration qui ne repose sur aucun contrôle du canal.
/// </param>
/// <param name="Steps">Le travail dû, un par système déclaré au moment de l'ouverture.</param>
public sealed record ClaimedRight(
  DataSubjectRight Right,
  ClaimState State,
  ClaimOrigin Origin,
  IdentityDeclaration IdentityAtOrigin,
  bool AwaitsConfirmation,
  bool MotivationIsDemanded,
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
/// service ne détient <b>aucun rattachement</b> dans ce système : six zéros ne doivent pas se lire
/// « cette personne n'est pas chez nous ».
/// <para>
/// ⚠️ <b>C'est une lecture de <see cref="Case.FindingIsDemandedBy"/>, et surtout pas un état
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
