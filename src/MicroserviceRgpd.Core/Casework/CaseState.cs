namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Où en est le dossier. <b>Deux valeurs, et il n'y en aura jamais une troisième nommée
/// « en retard »</b> : le dépassement du délai est un calcul fait à l'instant de l'affichage, jamais
/// un état qu'on atteint — sinon un retard non détecté deviendrait un retard inexistant, et la
/// preuve dépendrait de ce qu'une minuterie ait tourné.
/// </summary>
/// <remarks>
/// <b>La transition arrive avec la clôture</b>, qui détruit le nominatif à l'instant même et exige
/// une signature humaine. L'état existe dès maintenant parce que la file s'y appuie : une file qui
/// listerait « tous les dossiers » afficherait des dossiers clos le jour où la clôture existera, et
/// personne ne l'aurait décidé.
/// </remarks>
public sealed class CaseState : SmartEnum<CaseState>
{
  /// <summary>Ouvert. C'est l'état de naissance, et le seul que la file affiche.</summary>
  public static readonly CaseState Open = new(nameof(Open), 0, "ouvert");

  /// <summary>
  /// Clos, sous la signature d'un humain et par une cause qu'il a nommée. La clôture ne propage rien
  /// et ne gèle rien : un <see cref="Step"/> resté <see cref="StepState.ToDo"/> dans un dossier clos
  /// se lit comme un oubli.
  /// </summary>
  public static readonly CaseState Closed = new(nameof(Closed), 1, "clos");

  private CaseState(string name, int value, string frenchLabel)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }
}
