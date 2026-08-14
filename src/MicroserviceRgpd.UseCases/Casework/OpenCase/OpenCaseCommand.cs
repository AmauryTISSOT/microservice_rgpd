using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.EvidenceLog;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UseCases.Casework.OpenCase;

/// <summary>
/// Faire <b>entrer</b> une demande : un dossier s'ouvre, et le service cesse d'oublier.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un seul dossier, quels que soient les droits.</b> La commande porte un sac de désignations et
/// une collection de droits, et non une demande par droit : « lire avant d'effacer » traverse les
/// <c>Claim</c>, et deux dossiers rendraient l'invariant intenable.
/// </para>
/// <para>
/// <b>Aucun emplacement pour une pièce jointe.</b> Ni ici, ni sur aucun canal : aucune pièce
/// d'identité n'entre dans le service, et un champ qui pourrait en porter une serait un champ dans
/// lequel une pièce finirait par entrer.
/// </para>
/// <para>
/// <b>La date de réception est portée par la commande, et jamais devinée ici.</b> Chaque canal sait
/// ce qu'elle vaut chez lui et le dit : l'application poste à l'instant où elle reçoit, donc elle
/// <c>Declared</c> ; le dépôt manuel transcrit un courriel reçu il y a un nombre de jours inconnu,
/// donc il déclare ce que l'<c>Operator</c> a dit ou retombe sur le défaut. Un gestionnaire qui
/// aurait choisi à leur place aurait dû savoir de quel canal il était appelé.
/// </para>
/// </remarks>
/// <param name="IdentityDeclaration">
/// Ce que le canal d'entrée déclare de l'identité du demandeur. Le service l'enregistre et n'en
/// juge <b>jamais</b> la valeur.
/// </param>
/// <param name="Motivation">
/// Ce que l'humain a pesé avant d'ouvrir ces droits sous cette identité, ou <c>null</c> si personne
/// ne l'a pesé. <b>Le <c>null</c> n'est jamais refusé</b> : le service ne barre pas la route, et le
/// dossier restera visible comme faible.
/// </param>
/// <param name="Origin">D'où vient la reconnaissance de ces droits — une seule origine par dépôt.</param>
/// <param name="Reception">
/// Le jour de réception par le responsable de traitement, <b>et le régime sous lequel le canal le
/// sait</b> : déclaré par quelqu'un, ou tenu pour défaut. La paire ne se sépare pas en chemin.
/// </param>
/// <param name="Designations">
/// Le sac sous lequel on cherchera la personne. <b>Éventuellement vide</b> : une demande sans
/// aucune désignation est un dossier qu'on n'ouvrira nulle part, et c'est un fait que l'écran doit
/// pouvoir montrer plutôt qu'un refus qui renverrait la personne à son silence.
/// </param>
/// <param name="Rights">
/// Les droits reconnus à cette demande. Éventuellement vide, et jamais <c>OutOfScope</c>.
/// </param>
/// <param name="Signatory">Qui fait entrer la demande — l'application, ou un <c>Operator</c> nommé.</param>
public sealed record OpenCaseCommand(
  IdentityDeclaration IdentityDeclaration,
  IdentityMotivation? Motivation,
  IReadOnlyCollection<Designation> Designations,
  IReadOnlyCollection<DataSubjectRight> Rights,
  ClaimOrigin Origin,
  ReceptionDate Reception,
  Signatory Signatory)
  : ICommand<Result<Case>>;
