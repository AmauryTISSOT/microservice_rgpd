namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// L'issue qu'un humain a rendue sur une colonne, <b>et le moment où il l'a rendue</b> : l'état et
/// la date. Les deux entrent ensemble ou pas du tout.
/// </summary>
/// <remarks>
/// <para>
/// <b>C'est le type entier qui tient l'invariant.</b> Il n'existe ni <c>Retained</c> ni
/// <c>SetAside</c> sans date, parce qu'aucun chemin d'écriture ne pose un état sans passer par ici,
/// et qu'ici les deux champs sont exigés ensemble. Deux colonnes nullables sur la ligne auraient
/// dit la même chose et ne l'auraient pas tenue : <b>deux champs qu'un chemin d'écriture peut
/// dissocier finissent par se dissocier</b>. Même mécanique que le régime de <c>ReceptionDate</c>.
/// </para>
/// <para>
/// ⚠️ <b>Personne n'est enregistré ici, et c'est une décision.</b> Ce contexte ne garde pas qui a
/// arbitré — voir l'<c>ADR-0014</c>, qui porte l'asymétrie avec <c>Casework</c>, lequel enregistre
/// son signataire dans un <c>string?</c>. Un nom saisi sans authentification n'aurait été qu'une
/// déclaration de plus, et ce qui compte ici est <b>qu'un humain ait tranché</b> — la date le
/// prouve, la machine, elle, ne signe pas. Une date manquante n'est donc pas un champ vide, c'est
/// un arbitrage qui n'a pas eu lieu.
/// </para>
/// <para>
/// ⚠️ <b>Il n'y a pas d'histoire.</b> La trace est l'état courant seul, et un second arbitrage
/// l'écrase. Une histoire demanderait une troisième table, c'est-à-dire un <c>EvidenceLog</c> sous un
/// autre nom, au grain que ce contexte refuse — et un rapport de détection neuf détruit de toute
/// façon <i>tous</i>
/// les arbitrages, si bien qu'une histoire fine à l'intérieur d'un rapport serait une précision
/// absurde dans un dispositif qui jette le rapport complet. <b>Le coût est réel et déclaré</b> : un
/// <c>Operator</c> qui repasse une colonne de <c>Retained</c> à <c>SetAside</c> efface la date de
/// l'arbitrage qu'il remplace.
/// </para>
/// </remarks>
public sealed record Arbitration
{
  private Arbitration(ScreenedColumnState state, DateTimeOffset renderedOn)
  {
    State = state;
    RenderedOn = renderedOn;
  }

  /// <summary>L'issue rendue. Jamais <see cref="ScreenedColumnState.Awaiting"/> : personne ne tranche une absence de décision.</summary>
  public ScreenedColumnState State { get; }

  /// <summary>Quand l'humain l'a dit.</summary>
  public DateTimeOffset RenderedOn { get; }

  /// <summary>
  /// Porte l'issue d'un humain, ou refuse. <b>C'est le seul constructeur</b>, et il exige les deux
  /// à la fois.
  /// </summary>
  /// <param name="ruling">Retenue, ou écartée. Jamais <see cref="ScreenedColumnState.Awaiting"/>.</param>
  /// <param name="renderedOn">L'instant où il a tranché.</param>
  /// <exception cref="ArgumentNullException"><paramref name="ruling"/> est absent.</exception>
  /// <exception cref="ArgumentException">L'état n'est pas une issue.</exception>
  public static Arbitration Rendered(ScreenedColumnState ruling, DateTimeOffset renderedOn)
  {
    return new Arbitration(
      ScreenedColumnState.RulingOrThrow(ruling, nameof(ruling)),
      renderedOn);
  }
}
