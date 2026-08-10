namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// L'issue qu'un humain a rendue sur une colonne, <b>avec sa signature</b> : l'état, le nom qu'il a
/// saisi, et la date. Les trois entrent ensemble ou pas du tout.
/// </summary>
/// <remarks>
/// <para>
/// <b>C'est le type entier qui tient l'invariant.</b> Il n'existe ni <c>Retained</c> ni
/// <c>SetAside</c> non signé, parce qu'aucun chemin d'écriture ne pose un état sans passer par ici,
/// et qu'ici les trois champs sont exigés ensemble. Trois colonnes nullables sur la ligne auraient
/// dit la même chose et ne l'auraient pas tenue : <b>deux champs qu'un chemin d'écriture peut
/// dissocier finissent par se dissocier</b>. Même mécanique que le régime de <c>ReceptionDate</c>.
/// </para>
/// <para>
/// <b>Le nom est saisi, jamais authentifié</b> — il n'existe aucune authentification dans le
/// service, et la question « qui a le droit » n'a aucun mécanisme sur lequel se poser. C'est
/// <c>Greffier, pas témoin</c> : le service enregistre une déclaration et n'en juge jamais la
/// valeur. Il est en revanche <b>non nullable</b>, contre le <c>string?</c> de <c>Casework</c> : ici
/// une signature manquante n'est pas un champ vide, c'est un arbitrage qui n'a pas eu lieu.
/// </para>
/// <para>
/// ⚠️ <b>Il n'y a pas d'histoire.</b> La trace est l'état courant seul, et un second arbitrage
/// l'écrase. Une histoire demanderait une troisième table, c'est-à-dire un <c>Ledger</c> sous un
/// autre nom, au grain que ce contexte refuse — et un re-dépistage détruit de toute façon <i>tous</i>
/// les arbitrages, si bien qu'une histoire fine à l'intérieur d'un rapport serait une précision
/// absurde dans un dispositif qui jette le rapport complet. <b>Le coût est réel et déclaré</b> : un
/// <c>Operator</c> qui repasse une colonne de <c>Retained</c> à <c>SetAside</c> efface qui avait dit
/// quoi.
/// </para>
/// </remarks>
public sealed record Arbitration
{
  /// <summary>Le plafond du nom saisi, en unités UTF-16. C'est une signature, pas une prose.</summary>
  public const int MaxSignatoryLength = 100;

  private Arbitration(ScreenedColumnState state, string signedBy, DateTimeOffset signedOn)
  {
    State = state;
    SignedBy = signedBy;
    SignedOn = signedOn;
  }

  /// <summary>L'issue rendue. Jamais <see cref="ScreenedColumnState.Awaiting"/> : personne ne signe une absence de décision.</summary>
  public ScreenedColumnState State { get; }

  /// <summary>Le nom que l'humain a saisi. Recopié tel quel, jamais vérifié.</summary>
  public string SignedBy { get; }

  /// <summary>Quand il l'a dit.</summary>
  public DateTimeOffset SignedOn { get; }

  /// <summary>
  /// Porte l'issue d'un humain, ou refuse. <b>C'est le seul constructeur</b>, et il exige les trois
  /// à la fois.
  /// </summary>
  /// <param name="ruling">Retenue, ou écartée. Jamais <see cref="ScreenedColumnState.Awaiting"/>.</param>
  /// <param name="signedBy">Le nom saisi par celui qui tranche.</param>
  /// <param name="signedOn">L'instant où il a tranché.</param>
  /// <exception cref="ArgumentNullException"><paramref name="ruling"/> est absent.</exception>
  /// <exception cref="ArgumentException">L'état n'est pas une issue, ou la signature est vide, démesurée, ou porte un caractère de contrôle.</exception>
  public static Arbitration Rendered(ScreenedColumnState ruling, string? signedBy, DateTimeOffset signedOn)
  {
    return new Arbitration(
      ScreenedColumnState.RulingOrThrow(ruling, nameof(ruling)),
      ScreeningText.OrThrow(signedBy, "Le nom du signataire", MaxSignatoryLength, nameof(signedBy)),
      signedOn);
  }
}
