using MicroserviceRgpd.Core.Configuration;

namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// Le <b>système hôte</b>, tel que <c>Requests</c> l'appelle pour faire appliquer un droit : une
/// remise par le <b>canal d'exercice</b> que le Paramétrage associe au droit (ADR-0026, ADR-0028).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Il ne lève pas pour un échec de l'appel</b> : une réponse non 2xx, un délai dépassé ou une
/// erreur réseau sont des <see cref="HostSystemCall"/> comme les autres, que le journal d'exécution
/// retient. Seule une faute de programmation lève.
/// </para>
/// <para>
/// ⚠️ <b>Aucune nouvelle tentative</b> : un appel, une réponse. Aucun droit n'est appliqué sans le geste
/// de l'<c>Operator</c>.
/// </para>
/// </remarks>
public interface IHostSystem
{
  /// <summary>
  /// Demande au système hôte, <b>par <paramref name="channel"/></b>, d'appliquer le droit que porte
  /// <paramref name="body"/>, et rend ce que la remise a donné.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le port est typé sur le genre, non sur une espèce</b> : ses appelants ne connaissent qu'un
  /// geste — faire exercer un droit, par le canal déclaré —, et ne lisent jamais l'espèce du canal
  /// pour choisir comment. Ce filtrage vit en un seul endroit, dans l'adaptateur.
  /// </remarks>
  Task<HostSystemCall> ApplyAsync(ExerciseChannel channel, ExecutionBody body, CancellationToken cancellationToken);
}
