namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Où en est l'arbitrage d'une <see cref="ScreenedColumn"/>. Trois valeurs, et <b>aucune n'est
/// rendue par une machine</b> : le service signale, il ne retient ni n'écarte jamais.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="Awaiting"/> et <see cref="SetAside"/> sont repris mot pour mot de
/// <c>ReservationState</c></b> — même geste, même sens, et un synonyme inventé ferait croire à une
/// nuance qui n'existe pas. <c>Attached</c> ne transporte pas — rien n'est rattaché ici — et devient
/// <see cref="Retained"/>. Le mot est repris, jamais le type : l'identité de mot n'est pas une
/// identité de modèle, et factoriser sur elle serait la première fissure dans la clause
/// <c>Separate Ways</c>.
/// </para>
/// <para>
/// ⚠️ <b>Une colonne <c>Unflagged</c> est arbitrable comme les autres.</b> L'<c>Operator</c> peut la
/// passer en <see cref="Retained"/> de sa propre main, et c'est ce qui paye le fait de toutes les
/// rendre : sans ce geste, les lignes non signalées seraient neuf cents lignes grises qu'on survole,
/// et l'<c>Omission relue</c> serait décorative. Corollaire : un <see cref="Retained"/> posé sur une
/// colonne non signalée prouve qu'un <b>humain</b> l'a retenue, jamais que le service l'avait vue.
/// </para>
/// </remarks>
public sealed class ScreenedColumnState : SmartEnum<ScreenedColumnState>
{
  /// <summary>
  /// Personne n'a tranché. C'est l'état de naissance, et <b>le seul qui n'ait pas de date</b> :
  /// une date manquante n'est pas un champ vide, c'est un arbitrage qui n'a pas eu lieu.
  /// </summary>
  public static readonly ScreenedColumnState Awaiting = new(nameof(Awaiting), 0, "à arbitrer");

  /// <summary>Un humain a dit que cette colonne comptait. Il l'a dit à une date, et ce contexte ne retient pas qui il était.</summary>
  public static readonly ScreenedColumnState Retained = new(nameof(Retained), 1, "retenue", isSettled: true);

  /// <summary>
  /// Un humain a dit que cette colonne ne comptait pas. <b>Elle reste au rapport</b> : un arbitrage
  /// rendu n'est pas une ligne disparue.
  /// </summary>
  public static readonly ScreenedColumnState SetAside = new(nameof(SetAside), 2, "écartée", isSettled: true);

  private ScreenedColumnState(string name, int value, string frenchLabel, bool isSettled = false)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
    IsSettled = isSettled;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }

  /// <summary>Un humain a-t-il tranché ? C'est ce qui exige une date, et ce qui l'exige toujours.</summary>
  public bool IsSettled { get; }

  /// <summary>
  /// Cet état, si c'est une issue d'humain — sinon une programmation fautive nommée. Le garde vit
  /// ici plutôt que chez chaque appelant : ce qu'un développeur lirait à trois endroits doit être
  /// écrit à un seul.
  /// </summary>
  /// <exception cref="ArgumentNullException"><paramref name="state"/> est absent.</exception>
  /// <exception cref="ArgumentException">L'état n'est pas une issue : personne n'a tranché.</exception>
  public static ScreenedColumnState RulingOrThrow(ScreenedColumnState state, string parameterName)
  {
    ArgumentNullException.ThrowIfNull(state);

    if (!state.IsSettled)
    {
      throw new ArgumentException(
        $"« {state.Name} » n'est pas un arbitrage : c'est l'état d'une colonne que personne n'a "
        + "tranchée, et le service ne la tranche pas à la place de l'Operator.",
        parameterName);
    }

    return state;
  }
}
