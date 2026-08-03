using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Ledger;
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
/// <b>La date de réception n'est pas un paramètre de ce canal.</b> Une demande postée par
/// l'application arrive à l'instant où elle est postée ; c'est le dépôt manuel, où l'<c>Operator</c>
/// transcrit un courriel reçu il y a un nombre de jours inconnu, qui aura besoin de la déclarer.
/// </para>
/// </remarks>
/// <param name="IdentityDeclaration">
/// Ce que le canal d'entrée déclare de l'identité du demandeur. Le service l'enregistre et n'en
/// juge <b>jamais</b> la valeur.
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
  IReadOnlyCollection<Designation> Designations,
  IReadOnlyCollection<DataSubjectRight> Rights,
  Signatory Signatory)
  : ICommand<Result<Case>>;
