using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// La réclamation d'<b>un</b> <see cref="DataSubjectRight"/> à l'intérieur d'un <see cref="Case"/>.
/// C'est l'unité à laquelle le service répond à la personne au sujet d'un droit — elle atteste
/// l'<b>acte de répondre</b>, jamais le fait qu'un droit ait été satisfait.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Homonyme dormant</b> : <c>System.Security.Claims.Claim</c>. Le nom du glossaire l'emporte,
/// et aucun <c>using</c> de ce namespace n'entre dans ce contexte.
/// </para>
/// <para>
/// <b>Il n'est pas un agrégat, et n'a aucun dépôt.</b> Comme le <see cref="Step"/>, il naît, se
/// modifie et meurt par la racine — sans constructeur public ni propriété qu'on puisse écrire de
/// l'extérieur. La justification est l'invariant « lire avant d'effacer », qui traverse
/// <b>deux</b> <c>Claim</c> et qu'aucune frontière plus fine ne pourrait tenir.
/// </para>
/// <para>
/// <b>Un droit au plus par <see cref="Case"/>.</b> Deux <c>Claim</c> portant le même droit dans un
/// même dossier seraient deux réponses dues à la personne sur la même question — le droit
/// lui-même identifie donc la réclamation, et aucun identifiant de substitution n'est engendré.
/// </para>
/// </remarks>
public sealed class Claim
{
  private readonly List<Step> _steps;

  internal Claim(
    DataSubjectRight right,
    ClaimOrigin origin,
    IdentityDeclaration identityAtOrigin,
    IEnumerable<DeclaredSystemId> declaredSystems)
  {
    Right = right;
    State = ClaimState.Open;
    Origin = origin;
    IdentityAtOrigin = identityAtOrigin;
    Confirmed = origin.ConfirmedAtBirth;
    _steps = [.. declaredSystems.Select(system => new Step(system))];
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private Claim()
  {
    Right = DataSubjectRight.Access;
    State = ClaimState.Open;
    Origin = ClaimOrigin.Named;
    IdentityAtOrigin = IdentityDeclaration.Unverified;
    _steps = [];
  }

  /// <summary>
  /// Le droit réclamé. <b>Jamais <see cref="DataSubjectRight.OutOfScope"/></b> : celui-ci est le
  /// verdict qu'aucun droit n'a été reconnu, et une réclamation de rien n'existe pas. Une demande
  /// n'exerçant aucun droit ouvre un <see cref="Case"/> sans aucun <c>Claim</c>, et se clora
  /// <c>NotApplicable</c> sous la signature d'un humain.
  /// </summary>
  public DataSubjectRight Right { get; private set; }

  /// <summary>Où en est la réponse du service sur ce droit. Trois valeurs.</summary>
  public ClaimState State { get; private set; }

  /// <summary>
  /// Le travail dû, <b>un <see cref="Step"/> par <see cref="DeclaredSystem"/></b> du catalogue au
  /// moment où le dossier s'est ouvert. Éventuellement vide — c'est l'état d'un service dont le
  /// <c>Manifest</c> n'a pas encore été déclaré, et l'écran doit pouvoir le dire.
  /// </summary>
  public IReadOnlyList<Step> Steps => _steps;

  /// <summary>
  /// D'où vient la reconnaissance de ce droit. <b>Figée à la naissance</b> : un <c>Claim</c> garde
  /// la porte sous laquelle il est né.
  /// </summary>
  public ClaimOrigin Origin { get; private set; }

  /// <summary>
  /// Ce que le canal déclarait de l'identité du demandeur <b>à l'instant où ce droit s'est
  /// ouvert</b>, et non ce qu'il en déclare aujourd'hui.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>C'est une copie, et c'est le propos.</b> Le <see cref="Case"/> porte l'identité déclarée
  /// courante — l'identité est une propriété de la personne, et elle se reprend. Mais une déclaration
  /// relevée en fin de dossier ne réécrit pas la preuve d'hier : l'accès ouvert lundi l'a été sous
  /// <c>Unverified</c>, et le rappel passé vendredi ne le rend pas rétroactivement propre.
  /// </para>
  /// <para>
  /// La copie vit ici plutôt que dans le <c>Ledger</c> seul parce que l'écran doit pouvoir la
  /// montrer à côté du droit qu'elle concerne, sans faire relire à l'<c>Operator</c> la preuve pour
  /// savoir ce qu'il a sous les yeux.
  /// </para>
  /// </remarks>
  public IdentityDeclaration IdentityAtOrigin { get; private set; }

  /// <summary>
  /// Un humain a-t-il repris ce droit à son compte ? Vrai dès la naissance sauf pour
  /// <see cref="ClaimOrigin.Proposed"/>, qu'une machine seule a reconnu.
  /// </summary>
  public bool Confirmed { get; private set; }

  /// <summary>
  /// Le dossier doit-il <b>réclamer une motivation</b> pour ce droit ? C'est une lecture de
  /// <see cref="IdentityMotivation.IsDemandedBy"/> sur l'identité <b>d'origine</b>, jamais sur
  /// l'identité courante : ce qui est en cause est la porte sous laquelle ce droit s'est ouvert.
  /// </summary>
  public bool MotivationIsDemanded => IdentityMotivation.IsDemandedBy(IdentityAtOrigin, Right);

  /// <summary>
  /// Ce droit attend-il encore qu'un humain le reprenne à son compte ? Vrai d'un
  /// <see cref="ClaimOrigin.Proposed"/> non confirmé, et <b>de rien d'autre</b>.
  /// </summary>
  /// <remarks>
  /// C'est une <c>OpenQuestion</c>, jamais un blocage : le dossier est ouvert, le délai court, et
  /// l'attente est <b>visible dans le <see cref="Case"/></b> plutôt que rangée dans un vestibule que
  /// personne ne regarde.
  /// </remarks>
  public bool AwaitsConfirmation => !Confirmed;

  /// <summary>
  /// Un humain reprend ce droit à son compte. <b>Idempotent</b> : reconfirmer ne défait rien, et un
  /// second clic n'est pas une faute qu'il faudrait signaler à qui l'a fait.
  /// </summary>
  /// <remarks>
  /// <b>Elle ne consigne rien.</b> La ligne de preuve est écrite par l'appelant, hors de l'agrégat,
  /// pour la même raison qu'ailleurs : le <c>Ledger</c> survit au dossier de cinq ans.
  /// </remarks>
  internal void Confirm() => Confirmed = true;
}
