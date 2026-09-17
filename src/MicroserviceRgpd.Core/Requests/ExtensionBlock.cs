namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// Le <b>motif de blocage de la prolongation</b> : la raison pour laquelle la date limite de réponse
/// d'une <see cref="DataSubjectRequest"/> ne peut pas être reportée — <see cref="Closed"/>,
/// <see cref="AlreadyExtended"/> ou <see cref="DeadlineElapsed"/> (ADR-0029).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>L'ordre des valeurs est celui des motifs</b> : quand plusieurs conditions manquent, seule la
/// première se dit. Ce qui ferme la demande passe avant ce qui a déjà été posé sur elle, et ce qui a
/// déjà été posé avant ce que le calendrier a emporté.
/// </para>
/// <para>
/// ⚠️ <b>Ce n'est pas un <see cref="ExecutionBlock"/></b> : celui-là dit pourquoi une demande ne
/// s'exécute pas, face au Paramétrage et au déploiement. Celui-ci ne regarde que la demande et le
/// jour qu'il est — aucun de ses motifs ne nomme le droit, et aucun ne se règle ailleurs que sur la
/// demande.
/// </para>
/// <para>
/// ⚠️ <b>Ce n'est pas non plus un <see cref="ExtensionGround"/></b> : le motif de prolongation est
/// celui que l'<c>Operator</c> choisit pour prolonger ; celui-ci est celui que le serveur calcule
/// pour l'en empêcher.
/// </para>
/// </remarks>
public sealed class ExtensionBlock : SmartEnum<ExtensionBlock>
{
  /// <summary>La demande est Terminée ou Annulée : plus aucun délai ne court.</summary>
  public static readonly ExtensionBlock Closed = new(nameof(Closed), 0, "Demande close");

  /// <summary>
  /// La demande a déjà été prolongée. ⚠️ <b>Une seule fois</b> : l'article 12 §3 ouvre deux mois, et
  /// non deux mois renouvelables.
  /// </summary>
  public static readonly ExtensionBlock AlreadyExtended = new(nameof(AlreadyExtended), 1, "Demande déjà prolongée");

  /// <summary>
  /// La date limite de réponse est passée : on ne prolonge pas un délai déjà écoulé. ⚠️ <b>Le jour
  /// limite lui-même est accepté</b> — l'<c>Operator</c> ne perd pas le dernier jour que le règlement
  /// lui accorde.
  /// </summary>
  public static readonly ExtensionBlock DeadlineElapsed = new(nameof(DeadlineElapsed), 2, "Date limite de réponse dépassée");

  private ExtensionBlock(string name, int value, string frenchLabel)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
  }

  /// <summary>
  /// Le libellé destiné à l'<c>Operator</c>. ⚠️ <b>Il ne nomme aucun droit</b>, à la différence de
  /// deux des motifs de blocage de l'exécution : aucune de ces trois raisons ne dépend du droit
  /// invoqué.
  /// </summary>
  public string FrenchLabel { get; }
}
