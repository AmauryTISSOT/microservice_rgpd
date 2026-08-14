namespace MicroserviceRgpd.Core.Casework.EvidenceLog;

/// <summary>
/// Sous quel régime un <c>Operator</c> a signé — c'est-à-dire <b>ce que valait son nom au moment où
/// il l'a saisi</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un nom n'est pas une authentification, et le <c>EvidenceLog</c> le dit plutôt que de le taire.</b>
/// La surface de l'<c>Operator</c> n'authentifie personne — choix de PoC assumé et écrit —, et une
/// preuve qui garderait seulement le nom saisi serait relue dans dix ans comme si quelqu'un s'était
/// identifié.
/// </para>
/// <para>
/// <b>Une seule valeur aujourd'hui, et c'est exactement la raison d'être du type.</b> Le jour où la
/// GUI authentifiera son <c>Operator</c>, une seconde valeur entrera ici et les lignes d'hier
/// resteront lisibles pour ce qu'elles sont — sans quoi le <c>EvidenceLog</c> d'aujourd'hui serait
/// indiscernable de celui de demain.
/// </para>
/// </remarks>
public sealed class SignerVerification : SmartEnum<SignerVerification>
{
  /// <summary>
  /// Le nom a été <b>saisi, sans être vérifié par personne</b>. Le service enregistre ce qu'un
  /// humain a écrit ; il ne jure pas que ce soit le sien.
  /// </summary>
  public static readonly SignerVerification Unauthenticated =
    new(nameof(Unauthenticated), 0, "non authentifié");

  private SignerVerification(string name, int value, string frenchLabel)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
  }

  /// <summary>Le libellé destiné à l'humain qui relit la preuve. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }
}
