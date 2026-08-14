using MicroserviceRgpd.Core.Qualifications;

namespace MicroserviceRgpd.UnitTests;

/// <summary>
/// Les deux identités de moteur dont les tests ont besoin pour construire un avis.
/// </summary>
/// <remarks>
/// Elles sont ici parce qu'aucun test ne s'y intéresse : l'identité est une donnée que le service
/// conserve, jamais une donnée dont il décide. La rendre disponible d'un mot évite qu'elle
/// n'encombre les tests qui vérifient autre chose.
/// </remarks>
public static class AnEngine
{
  /// <summary>L'identité déclarée par le moteur qui tient le rôle de verdict.</summary>
  public static readonly QualificationEngineIdentity HoldingTheVerdict = new("llm", "qwen3:8b");

  /// <summary>L'identité déclarée par le moteur qui tient le rôle de lexique.</summary>
  public static readonly QualificationEngineIdentity HoldingTheLexicon = new("lexicon", "1.0.0");
}
