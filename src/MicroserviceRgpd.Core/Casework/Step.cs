namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Le travail dû sur <b>un</b> <see cref="DeclaredSystem"/> pour un <see cref="Claim"/> donné.
/// <para>
/// <b>Son grain est le système, jamais la <see cref="Capability"/></b> : l'<c>Operator</c> a besoin
/// de savoir où en est son accès <b>chez tel prestataire</b>, jamais où en est le <c>Locate</c>.
/// Un grain à la capacité ferait remonter à l'écran une mécanique interne à la place du travail.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Il n'est pas un agrégat, et n'a aucun dépôt.</b> Il naît par le <see cref="Case"/>, se
/// modifie par le <see cref="Case"/>, et disparaît avec lui. La règle est structurelle plutôt que
/// disciplinaire : ce type ne porte aucun constructeur public et aucune propriété qu'on puisse
/// écrire de l'extérieur, et rien ne le désigne comme racine.
/// </para>
/// <para>
/// <b>Il naît par (<see cref="Claim"/>, <see cref="DeclaredSystem"/>)</b>, et il naît
/// <see cref="StepState.ToDo"/> — un état qui, resté tel dans un dossier clos, se lit comme un
/// oubli plutôt que comme un neutre rassurant.
/// </para>
/// </remarks>
public sealed class Step
{
  internal Step(DeclaredSystemId declaredSystem)
  {
    DeclaredSystem = declaredSystem;
    State = StepState.ToDo;
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private Step()
  {
    State = StepState.ToDo;
  }

  /// <summary>
  /// Le système sur lequel ce travail est dû, par l'identifiant que l'<c>Adapter</c> recevra.
  /// <b>Une référence, jamais une copie</b> : le <c>Manifest</c> vieillit exprès, et le figer ici
  /// ferait de chaque dossier un second catalogue à tenir.
  /// </summary>
  public DeclaredSystemId DeclaredSystem { get; private set; }

  /// <summary>Où en est ce travail. Cinq valeurs, dont deux qui refusent de fusionner.</summary>
  public StepState State { get; private set; }
}
