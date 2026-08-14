using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.Core.Qualifications.Audit;

namespace MicroserviceRgpd.Infrastructure.Data.Audit;

/// <summary>
/// Écrit la trace, et rien d'autre.
/// </summary>
/// <remarks>
/// <para>
/// <b>Il ne passe pas par le dépôt générique.</b> Celui-ci est contraint aux agrégats racines, et
/// l'emprunter aurait déclaré la trace agrégat — exactement ce que la conception a refusé. Il écrit
/// donc directement dans le contexte, ce qui est le privilège d'un adaptateur d'infrastructure.
/// </para>
/// <para>
/// <b>Il n'attrape rien.</b> Une base indisponible est une panne du service : l'échec remonte tel
/// quel jusqu'au <c>500</c>, et il n'y a ici aucun repli qui pourrait rendre <c>200</c> à un
/// appelant dont l'acte n'a laissé aucun écrit.
/// </para>
/// </remarks>
public sealed class QualificationAuditTrail(AppDbContext dbContext) : IQualificationAuditTrail
{
  /// <inheritdoc />
  public async Task RecordAsync(QualificationAuditEntry entry, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(entry);

    dbContext.QualificationAuditEntries.Add(RowOf(entry));

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  /// <summary>
  /// Met l'écrit à plat. C'est la seule traduction du parcours, et elle ne décide de rien : ce qui
  /// est nul dans l'entrée l'est dans la ligne, et c'est cette nullité qui enregistre la dégradation.
  /// </summary>
  private static QualificationAuditRow RowOf(QualificationAuditEntry entry)
  {
    return new QualificationAuditRow
    {
      QualificationId = entry.QualificationId,
      // L'instant est ramené en UTC : `timestamptz` ne conserve pas le décalage, et laisser passer
      // une heure locale ferait dépendre la trace du fuseau de la machine qui l'a écrite.
      OccurredAt = entry.OccurredAt.ToUniversalTime(),
      Text = entry.Text.Value,
      Rights = NamesOf(entry.Qualification),
      ReviewSignal = entry.ReviewSignal.ToString(),
      VerdictRights = entry.VerdictOpinion is null ? null : NamesOf(entry.VerdictOpinion.Qualification),
      VerdictDeclaredConfidence = entry.VerdictOpinion?.DeclaredConfidence?.ToString(),
      VerdictEngineName = entry.VerdictOpinion?.Engine.Name,
      VerdictEngineVersion = entry.VerdictOpinion?.Engine.Version,
      LexiconRights = entry.LexiconOpinion is null ? null : NamesOf(entry.LexiconOpinion.Qualification),
      LexiconDeclaredConfidence = entry.LexiconOpinion?.DeclaredConfidence?.ToString(),
      LexiconEngineName = entry.LexiconOpinion?.Engine.Name,
      LexiconEngineVersion = entry.LexiconOpinion?.Engine.Version,
      Justification = entry.Justification,
      CallerReference = entry.CallerReference,
      TraceId = entry.TraceId,
      TotalLatencyMs = MillisecondsOf(entry.TotalLatency),
      VerdictLatencyMs = entry.VerdictLatency is { } verdict ? MillisecondsOf(verdict) : null,
      LexiconLatencyMs = entry.LexiconLatency is { } lexicon ? MillisecondsOf(lexicon) : null,
    };
  }

  /// <summary>
  /// Les droits sous leurs noms canoniques, dans l'ordre de la taxonomie.
  /// </summary>
  /// <remarks>
  /// Le domaine les tient pour un ensemble, sans ordre signifiant ; en figer un ici évite que deux
  /// lignes identiques se lisent différemment, et que la comparaison de deux avis passe par un tri.
  /// </remarks>
  private static string[] NamesOf(Qualification qualification)
  {
    return [.. qualification.Rights.OrderBy(right => right.Value).Select(right => right.Name)];
  }

  /// <summary>
  /// La durée en millisecondes entières. Bornée plutôt que débordante : une mesure aberrante ne doit
  /// pas empêcher d'écrire la trace du verdict qu'elle accompagne.
  /// </summary>
  private static int MillisecondsOf(TimeSpan latency)
  {
    return (int)Math.Clamp(Math.Round(latency.TotalMilliseconds), 0, int.MaxValue);
  }
}
