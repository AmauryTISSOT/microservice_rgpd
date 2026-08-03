using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Infrastructure.Qualifications;

/// <summary>
/// Le moteur qui rend le verdict, vu du domaine : un LLM local qui vit derrière le sidecar Python et
/// qu'on joint en HTTP, exactement comme le lexique et <b>derrière le même port</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cet adaptateur est du transport, et rien de plus.</b> Il ne qualifie rien, ne pondère rien,
/// n'arbitre rien : il porte un texte jusqu'au moteur et rapporte un avis. Toute la matière de
/// décision — la corroboration, le repli — vit dans le domaine, hors d'atteinte de HTTP.
/// </para>
/// <para>
/// Il <b>re-porte les invariants</b> que le sidecar tient déjà — par le chemin que les deux moteurs
/// partagent — et deux de plus qui n'appartiennent qu'à lui : la confiance et la justification sont
/// <b>obligatoires ici</b>. Un avis sans
/// confiance rendrait la corroboration aveugle au seul bit qu'elle lit d'elle ; un avis sans
/// justification priverait l'opérateur du seul texte sur lequel il relit. Ni l'un ni l'autre n'est
/// un avis faible : c'est une panne du moteur, et elle se présente comme telle.
/// </para>
/// </remarks>
public sealed class LlmQualificationEngine(HttpClient client) : IQualificationEngine
{
  /// <summary>Le point d'entrée du moteur qui a un amont — donc des pannes que le lexique n'a pas.</summary>
  private const string Endpoint = "opinions/llm";

  /// <summary>Le nom sous lequel ce moteur se déclare en panne, pour que l'exploitant sache où réparer.</summary>
  private const string Engine = "Le moteur LLM";

  /// <inheritdoc />
  public async Task<QualificationOpinion> QualifyAsync(
    RightsRequestText text,
    CancellationToken cancellationToken = default)
  {
    return await SidecarExchange.AskAsync<LlmOpinionResponse>(
      client, Endpoint, Engine, text, Read, cancellationToken);
  }

  /// <summary>
  /// Fait de l'avis arrivé un avis du domaine, ou nomme ce qui manque — les deux exigences que ce
  /// moteur porte en propre.
  /// </summary>
  private static QualificationOpinion Read(LlmOpinionResponse opinion)
  {
    // Un degré absent ne se replie pas sur « basse » : un repli inventerait une auto-évaluation que
    // le modèle n'a pas rendue, et c'est précisément sur elle que l'opérateur trie sa relecture.
    // Un degré hors de l'échelle, lui, a déjà été refusé à la lecture du fil.
    if (opinion.Confidence is null)
    {
      throw new QualificationEngineFailure(
        $"{Engine} a rendu un avis sans confiance déclarée, alors qu'il en déclare toujours une.");
    }

    if (string.IsNullOrWhiteSpace(opinion.Justification))
    {
      throw new QualificationEngineFailure(
        $"{Engine} a rendu un avis sans justification, alors que c'est ce qu'il apporte de plus que le lexique.");
    }

    return new QualificationOpinion(
      SidecarExchange.VerdictOf(opinion.Rights, Engine),
      SidecarExchange.IdentityOf(opinion.Engine, Engine),
      opinion.Confidence,
      opinion.Justification);
  }

  /// <summary>
  /// L'avis tel qu'il arrive, <b>avec le moteur qui l'a rendu</b>. C'est ici que la version compte le
  /// plus : elle dit de quel modèle servi relève une qualification, et la trace d'audit la conserve.
  /// </summary>
  private sealed record LlmOpinionResponse(
    IReadOnlyList<DataSubjectRight>? Rights,
    DeclaredConfidence? Confidence,
    string? Justification,
    SidecarExchange.EngineResponse? Engine);
}
