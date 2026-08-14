namespace MicroserviceRgpd.Core.Casework.EvidenceLog;

/// <summary>
/// Qui a signé une ligne du <c>EvidenceLog</c> : un humain nommé, ou le canal applicatif — c'est-à-dire
/// <b>personne</b>.
/// </summary>
/// <remarks>
/// <b>Les deux ne se confondent pas, et l'absence d'humain ne s'écrit pas par un nom vide.</b> Le
/// <c>EvidenceLog</c> prouve « par qui », et un champ laissé vide se lirait comme un nom qu'on a oublié
/// de saisir là où il s'agit d'un fait : aucun humain n'a signé cette ligne-là, et il n'y avait
/// aucune raison qu'il en signe une.
/// </remarks>
public sealed class SignatoryKind : SmartEnum<SignatoryKind>
{
  /// <summary>
  /// L'application du client, appelant depuis une session qu'elle a elle-même authentifiée. Aucun
  /// humain n'a signé : la seule chose que le service puisse prouver est que l'appel est entré.
  /// </summary>
  public static readonly SignatoryKind Application = new(nameof(Application), 0, "l'application du client");

  /// <summary>
  /// Un <c>Operator</c>, nommé. C'est ce nom que le <c>EvidenceLog</c> garde définitivement — « par
  /// qui » étant un tiers de ce que le service prouve.
  /// </summary>
  public static readonly SignatoryKind Operator = new(nameof(Operator), 1, "un opérateur");

  private SignatoryKind(string name, int value, string frenchLabel)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
  }

  /// <summary>Le libellé destiné à l'humain qui relit la preuve. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }
}
