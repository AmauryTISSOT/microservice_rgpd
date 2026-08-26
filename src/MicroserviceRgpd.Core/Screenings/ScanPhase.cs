namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Où en est un <c>Scan</c>, et — quand il tombe — où il est tombé. Quatre phases, dans l'ordre où
/// l'<c>Operator</c> les traverse.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>La quatrième phase n'appartient pas au scan, et c'est voulu.</b> La détection existe aussi
/// sur le chemin collé : le <c>IDatabaseScanner</c> ne l'atteint jamais et ne rendra jamais
/// <see cref="Screening"/>. Elle vit ici parce que l'écran d'attente couvre <b>une phase de plus</b>
/// que le scan — voir <c>ScanProgress</c> au glossaire —, et qu'un second type pour une seule valeur
/// obligerait chaque écran à en lire deux.
/// </para>
/// <para>
/// ⚠️ <b>Elles ne sont pas un état.</b> Un <c>Screening</c> n'a aucun état ; ce qui progresse est le
/// transitoire qui vit à côté. Une phase dit ce que le service est en train de faire, jamais ce
/// qu'un objet enregistré est devenu.
/// </para>
/// </remarks>
public sealed class ScanPhase : SmartEnum<ScanPhase>
{
  public static readonly ScanPhase Connecting = new(nameof(Connecting), 1, "connexion");

  public static readonly ScanPhase Cataloguing = new(nameof(Cataloguing), 2, "relevé du schéma");

  public static readonly ScanPhase Sampling = new(nameof(Sampling), 3, "aperçus");

  public static readonly ScanPhase Detecting = new(nameof(Detecting), 4, "détection");

  private ScanPhase(string name, int value, string frenchLabel)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
  }

  /// <summary>Ce que l'écran d'attente nomme.</summary>
  public string FrenchLabel { get; }
}
