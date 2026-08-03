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
}
