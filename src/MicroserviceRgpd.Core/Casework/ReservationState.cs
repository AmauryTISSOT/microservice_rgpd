namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Où en est l'arbitrage d'une <see cref="Reservation"/>. Trois valeurs, et <b>aucune n'est rendue
/// par une machine</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Les deux issues ne se confondent pas, et le neutre n'en est pas une.</b>
/// <see cref="Awaiting"/> dit que <b>personne n'a encore tranché</b> — c'est l'état de naissance, et
/// c'est celui qui reste visible dans le dossier. Le confondre avec <see cref="SetAside"/> ferait
/// écarter par défaut la donnée de quelqu'un que personne n'a regardée : très exactement
/// l'<c>Omission silencieuse</c>, sous la forme la plus commode.
/// </para>
/// <para>
/// ⚠️ <b>Aucune valeur ne se pose toute seule.</b> Ni la fusion à tort — irréversible, et portant sur
/// la donnée d'un tiers — ni l'exclusion par prudence n'appartiennent au service : l'<c>Operator</c>
/// est seul à produire une issue, et la sienne est nommée et datée au <c>EvidenceLog</c>.
/// </para>
/// </remarks>
public sealed class ReservationState : SmartEnum<ReservationState>
{
  /// <summary>
  /// Personne n'a tranché. C'est l'état de naissance, et il <b>reste affiché</b> : une réserve non
  /// arbitrée est une question ouverte dans le dossier, jamais un silence.
  /// </summary>
  public static readonly ReservationState Awaiting = new(nameof(Awaiting), 0, "à arbitrer");

  /// <summary>
  /// Un humain a dit que c'était bien la personne. Si la réserve portait des
  /// <see cref="Designation"/>, elles <b>entrent au sac</b> et serviront les appels suivants.
  /// </summary>
  public static readonly ReservationState Attached = new(nameof(Attached), 1, "rattachée", isSettled: true);

  /// <summary>
  /// Un humain a dit que ce n'était pas la personne. <b>Rien n'entre au sac</b>, et le fait reste
  /// écrit : une réserve écartée est un arbitrage rendu, non une réserve disparue.
  /// </summary>
  public static readonly ReservationState SetAside = new(nameof(SetAside), 2, "écartée", isSettled: true);

  private ReservationState(string name, int value, string frenchLabel, bool isSettled = false)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
    IsSettled = isSettled;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }

  /// <summary>
  /// Un humain a-t-il tranché ? C'est ce qui décide qu'une réserve cesse d'être réclamée à l'écran,
  /// et qu'un second arbitrage ne réécrit pas le premier.
  /// </summary>
  public bool IsSettled { get; }

  /// <summary>
  /// Cet état, si c'est une issue d'humain — sinon une programmation fautive nommée. Le garde vit
  /// ici plutôt que chez chaque appelant : ce qu'un développeur lirait à trois endroits doit être
  /// écrit à un seul.
  /// </summary>
  /// <exception cref="ArgumentNullException"><paramref name="state"/> est absent.</exception>
  /// <exception cref="ArgumentException">L'état n'est pas une issue : personne n'a tranché.</exception>
  public static ReservationState RulingOrThrow(ReservationState state, string parameterName)
  {
    ArgumentNullException.ThrowIfNull(state);

    if (!state.IsSettled)
    {
      throw new ArgumentException(
        $"« {state.Name} » n'est pas un arbitrage : c'est l'état d'une réserve que personne n'a "
        + "tranchée, et le service ne la tranche pas à la place de l'Operator.",
        parameterName);
    }

    return state;
  }
}
