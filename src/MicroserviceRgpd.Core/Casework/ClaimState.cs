namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Où en est la réponse du service à la personne au sujet d'<b>un</b> droit. Trois valeurs.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="Answered"/> atteste que le service a répondu, jamais que le droit a été
/// satisfait.</b> Un <see cref="Claim"/> se clôt <see cref="Answered"/> alors même que des
/// <see cref="Step"/> sont restés inatteints : l'incomplétude reste visible dans les
/// <see cref="Step"/> plutôt que masquée par un état de haut niveau rassurant.
/// </para>
/// <para>
/// Les noms écartés le sont pour cette raison exacte — <c>Honoured</c>, <c>Fulfilled</c>,
/// <c>Satisfied</c>, <c>Completed</c> promettent tous dans leur nom même une chose que le service
/// ne prouve jamais.
/// </para>
/// </remarks>
public sealed class ClaimState : SmartEnum<ClaimState>
{
  /// <summary>Ouvert. C'est l'état de naissance : le service n'a pas encore répondu sur ce droit.</summary>
  public static readonly ClaimState Open = new(nameof(Open), 0, "ouvert");

  /// <summary>
  /// Répondu — l'<b>acte de répondre</b>, et lui seul. Compatible avec des <see cref="Step"/>
  /// inatteints, qui restent lisibles un par un.
  /// </summary>
  public static readonly ClaimState Answered = new(nameof(Answered), 1, "répondu");

  /// <summary>
  /// Refusé. Distinct par sa <b>charge probatoire propre</b> et par la motivation humaine qu'il
  /// exige — les mentions de l'art. 12.4 sont dues à la personne.
  /// </summary>
  public static readonly ClaimState Refused = new(nameof(Refused), 2, "refusé");

  private ClaimState(string name, int value, string frenchLabel)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }
}
