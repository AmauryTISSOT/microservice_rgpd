namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Ce que le canal d'entrée a <b>déclaré</b> sur l'identité du demandeur. Vocabulaire fermé de
/// quatre valeurs, porté par le <see cref="Case"/> — l'identité est une propriété de la personne,
/// jamais d'un droit.
/// <para>
/// <b>Le service enregistre la déclaration et n'en juge jamais la valeur</b> ; il ne vérifie
/// lui-même aucune identité. Greffier, pas témoin.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Le vocabulaire est <b>fermé pour qu'on puisse compter</b> : le contrôle juge une pratique en
/// dénombrant des valeurs, jamais en lisant de la prose.
/// </para>
/// <para>
/// ⚠️ <b>Aucune pièce d'identité n'entre dans le service</b>, tous canaux confondus. Le
/// <c>Ledger</c> consigne le fait qu'une pièce est passée, jamais la pièce.
/// </para>
/// </remarks>
public sealed class IdentityDeclaration : SmartEnum<IdentityDeclaration>
{
  /// <summary>
  /// La demande est arrivée depuis une session authentifiée de l'application du client. Le
  /// <see cref="Case"/> la porte <b>sans vérification supplémentaire de l'appelant</b> : c'est
  /// l'application qui a authentifié, et le service ne refait pas son travail.
  /// </summary>
  public static readonly IdentityDeclaration ApplicationSession =
    new(nameof(ApplicationSession), 0, "session authentifiée de l'application");

  /// <summary>
  /// Le canal lui-même vaut contrôle — un espace client, un guichet. Inatteignable sur bien des
  /// installations, et la valeur existe quand même : ce n'est pas parce qu'un client ne peut pas
  /// l'atteindre qu'un autre ne l'atteindra jamais.
  /// </summary>
  public static readonly IdentityDeclaration ChannelControl =
    new(nameof(ChannelControl), 1, "contrôle par le canal");

  /// <summary>L'<c>Operator</c> atteste l'identité — reconnaissance personnelle, recoupement, rappel.</summary>
  public static readonly IdentityDeclaration OperatorAttested =
    new(nameof(OperatorAttested), 2, "attestée par l'opérateur");

  /// <summary>
  /// Rien n'a été vérifié. <b>La valeur laide doit exister</b> : sans elle, l'opérateur pressé
  /// coche la valeur propre et le service fabrique un faux au lieu d'enregistrer un vide.
  /// </summary>
  public static readonly IdentityDeclaration Unverified =
    new(nameof(Unverified), 3, "non vérifiée");

  private IdentityDeclaration(string name, int value, string frenchLabel)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }
}
