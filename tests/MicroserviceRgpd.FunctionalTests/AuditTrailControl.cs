using MicroserviceRgpd.Core.Qualifications.Audit;

namespace MicroserviceRgpd.FunctionalTests;

/// <summary>
/// De quoi faire echouer l ecriture de la trace, et rien de plus.
/// </summary>
/// <remarks>
/// La panne se dicte ici plutot qu en arretant le conteneur PostgreSQL : ce qui se verifie est ce
/// que le service rend quand l ecriture echoue, non la facon dont une base tombe.
/// </remarks>
public sealed class AuditTrailControl
{
  /// <summary>Ce qui fera echouer la prochaine ecriture, ou rien pour laisser ecrire.</summary>
  public Exception? Refusal { get; set; }

  /// <summary>Ramene la trace a un etat qui laisse ecrire.</summary>
  public void Reset() => Refusal = null;
}

/// <summary>
/// La vraie trace d audit, sous surveillance : elle ecrit reellement, sauf quand un test lui dicte
/// de refuser.
/// </summary>
/// <remarks>
/// Elle enveloppe l adaptateur reel plutot que de le remplacer : sans cela, les tests de bout en
/// bout ne verifieraient plus que quelque chose s ecrit vraiment dans PostgreSQL avant la reponse.
/// </remarks>
internal sealed class SupervisedAuditTrail(IQualificationAuditTrail inner, AuditTrailControl control)
  : IQualificationAuditTrail
{
  public Task RecordAsync(QualificationAuditEntry entry, CancellationToken cancellationToken = default)
  {
    return control.Refusal is { } refusal
      ? Task.FromException(refusal)
      : inner.RecordAsync(entry, cancellationToken);
  }
}
