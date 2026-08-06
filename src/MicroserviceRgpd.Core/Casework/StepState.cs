namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Où en est le travail dû sur <b>un</b> <see cref="DeclaredSystem"/> pour un <see cref="Claim"/>
/// donné. Cinq valeurs, et deux d'entre elles refusent de fusionner.
/// </summary>
/// <remarks>
/// <b><see cref="OutOfReach"/> et <see cref="Untreated"/> ne fusionnent pas.</b> Le premier est
/// <b>structurel et annonçable au premier jour</b> — une comptabilité scellée dix ans ne pourra
/// jamais effacer — le second est un <b>aveu constaté à la fin</b>. Ils n'ont ni le même lecteur —
/// la réponse à la personne d'un côté, la preuve destinée au contrôle de l'autre — ni la même
/// vérité dans le temps. Les fondre en un « non fait » ferait disparaître la seule trace que
/// l'<c>Omission silencieuse</c> laisse jamais.
/// </remarks>
public sealed class StepState : SmartEnum<StepState>
{
  /// <summary>À faire. C'est l'état de naissance, et <b>rester ainsi dans un dossier clos se lit comme un oubli</b>.</summary>
  public static readonly StepState ToDo = new(nameof(ToDo), 0, "à faire");

  /// <summary>L'<c>Adapter</c> a répondu <c>202</c> et déclaré une échéance ; le service reviendra à l'ouverture du dossier.</summary>
  public static readonly StepState Awaiting = new(nameof(Awaiting), 1, "en attente");

  /// <summary>
  /// Fait — <b>déclaré, jamais vérifié</b>. Le service prouve qu'on a déclaré l'avoir fait ; il ne
  /// jure pas que ce soit vrai, n'en ayant aucun moyen et ne prétendant pas en avoir un.
  /// </summary>
  /// <remarks>
  /// C'est le seul état sur lequel un <b>constat puisse être réclamé</b> — voir
  /// <see cref="DemandsAFinding"/>.
  /// </remarks>
  public static readonly StepState Done = new(nameof(Done), 2, "fait");

  /// <summary>
  /// Hors d'atteinte, <b>structurellement</b> : ce système ne pourra jamais servir ce droit, et on
  /// le sait dès le premier jour. Fige ce que le <c>Manifest</c> disait au moment du <c>Case</c>.
  /// </summary>
  public static readonly StepState OutOfReach = new(nameof(OutOfReach), 3, "hors d'atteinte");

  /// <summary>
  /// Non traité — l'<b>aveu constaté à la fin</b> : c'était possible, personne ne l'a fait. C'est
  /// la trace la plus précieuse du dispositif, et le service n'a jamais le droit de la bloquer.
  /// </summary>
  public static readonly StepState Untreated = new(nameof(Untreated), 4, "non traité");

  private StepState(string name, int value, string frenchLabel)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }

  /// <summary>
  /// Cet état <b>réclame-t-il un constat</b> de l'humain qui le déclare ? Vrai d'un
  /// <see cref="Done"/> <b>à zéro rattachement</b>, et de rien d'autre.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Pourquoi <c>Done</c> à zéro, et lui seul.</b> Un « fait » pour lequel le service ne détient
  /// aucun rattachement est la forme la plus dangereuse de l'<c>Omission silencieuse</c> : six zéros
  /// se liraient « cette personne n'est pas chez nous » alors que personne ne l'a établi. Le constat
  /// est donc exigé là, et nulle part ailleurs — exiger une prose sur chaque état ferait écrire une
  /// ligne de rien à chaque clic, et le constat qui compte se noierait dans les autres. Un système où
  /// le <c>Locate</c> a rattaché quelque chose n'a pas ce besoin : le rattachement <b>est</b> le
  /// dénominateur que le constat devait fournir.
  /// </para>
  /// <para>
  /// <b>Ce n'est pas un sixième état</b>, et il n'y en aura pas : c'est une lecture de l'état
  /// existant, faite par l'écran pour réclamer et par la ligne de preuve pour refuser une déclaration
  /// que personne n'a motivée. Une seule règle, aux deux endroits.
  /// </para>
  /// <para>
  /// ⚠️ <b>Une réserve que personne n'a tranchée ne compte pas comme un rattachement.</b> Voir
  /// <see cref="Locating.HoldsAnAttachment"/> : compter comme trouvé ce que personne n'a regardé
  /// lèverait l'exigence au moment précis où elle vaut le plus.
  /// </para>
  /// </remarks>
  /// <param name="anAttachmentIsHeld">
  /// Le service détient-il un rattachement dans le système dont on déclare le travail dû ?
  /// </param>
  public bool DemandsAFinding(bool anAttachmentIsHeld) => this == Done && !anAttachmentIsHeld;
}
