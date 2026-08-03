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

  internal Claim(DataSubjectRight right, IEnumerable<DeclaredSystemId> declaredSystems)
  {
    Right = right;
    State = ClaimState.Open;
    _steps = [.. declaredSystems.Select(system => new Step(system))];
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private Claim()
  {
    Right = DataSubjectRight.Access;
    State = ClaimState.Open;
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
}
