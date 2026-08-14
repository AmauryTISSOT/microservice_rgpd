namespace MicroserviceRgpd.Infrastructure.Data.Audit;

/// <summary>
/// La ligne de trace telle qu'elle est écrite : la projection à plat de
/// <see cref="Core.Qualifications.Audit.QualificationAuditEntry"/> sur la seule table du dépôt.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle est confinée à l'infrastructure, et n'est jamais déclarée agrégat racine.</b> Le dépôt
/// générique du gabarit est contraint à <c>IAggregateRoot</c> : marquer cette classe l'aurait fait
/// s'appliquer à elle, et aurait déclaré agrégat ce qui n'est que l'écrit d'un acte. Le dépôt
/// générique et l'interface d'agrégat restent intacts pour un vrai agrégat, le jour où il en
/// apparaîtra un.
/// </para>
/// <para>
/// <b>Aucune règle ne vit ici.</b> Les invariants du domaine sont portés par les types du domaine ;
/// cette classe n'existe que pour qu'EF Core ait des colonnes à remplir, et une ligne relue ne
/// remonte jamais vers le domaine — rien ne la relit.
/// </para>
/// <para>
/// <b>Les deux avis sont nommés par leur rôle, jamais par leur technologie</b> — le nom du moteur
/// est sur la ligne, à côté de son avis, et c'est lui qui dit qu'un lexique ou un LLM a parlé. Un
/// nommage par technologie aurait fait mentir la table le jour où les rôles s'échangent, ce qui est
/// une décision d'enregistrement de services et non de schéma.
/// </para>
/// </remarks>
public sealed class QualificationAuditRow
{
  /// <summary>L'identité de la qualification, et la clé de la trace.</summary>
  public required Guid QualificationId { get; init; }

  /// <summary>L'instant de l'acte, en UTC.</summary>
  public required DateTimeOffset OccurredAt { get; init; }

  /// <summary>Le texte qualifié, intégral et en clair.</summary>
  public required string Text { get; init; }

  /// <summary>
  /// Le verdict rendu, aux noms canoniques anglais de la taxonomie. <b>Jamais nul</b> : c'est ce qui
  /// rend impossible une ligne sans verdict, et donc une trace de tentative.
  /// </summary>
  public required string[] Rights { get; init; }

  /// <summary>L'urgence à relire, par son nom.</summary>
  public required string ReviewSignal { get; init; }

  /// <summary>L'avis du moteur de verdict, ou <c>null</c> — et c'est le repli sur le lexique.</summary>
  public string[]? VerdictRights { get; init; }

  /// <summary>La confiance que ce moteur a déclarée, par son nom, s'il en déclare une.</summary>
  public string? VerdictDeclaredConfidence { get; init; }

  /// <summary>Le nom du moteur qui a tenu le rôle de verdict.</summary>
  public string? VerdictEngineName { get; init; }

  /// <summary>La version que ce moteur a déclarée d'elle-même.</summary>
  public string? VerdictEngineVersion { get; init; }

  /// <summary>L'avis du moteur lexical, ou <c>null</c> — et c'est un verdict resté sans contrôle.</summary>
  public string[]? LexiconRights { get; init; }

  /// <summary>
  /// La confiance que le moteur lexical a déclarée, s'il en déclare une — le lexique n'en déclare
  /// aucune, mais la colonne existe parce que rien dans le domaine n'interdit à un lexique d'en
  /// avoir : la perdre en silence le jour où les rôles changent priverait la trace d'une prémisse.
  /// </summary>
  public string? LexiconDeclaredConfidence { get; init; }

  /// <summary>Le nom du moteur qui a tenu le rôle de lexique.</summary>
  public string? LexiconEngineName { get; init; }

  /// <summary>La version que ce moteur a déclarée d'elle-même.</summary>
  public string? LexiconEngineVersion { get; init; }

  /// <summary>La phrase rendue à l'opérateur, quand il y en a eu une.</summary>
  public string? Justification { get; init; }

  /// <summary>La référence de l'appelant, conservée verbatim.</summary>
  public string? CallerReference { get; init; }

  /// <summary>La trace de télémétrie de l'échange, quand la propagation en a fourni une.</summary>
  public string? TraceId { get; init; }

  /// <summary>Le temps qu'a pris la qualification entière, en millisecondes.</summary>
  public required int TotalLatencyMs { get; init; }

  /// <summary>Le temps qu'a pris le moteur de verdict, ou rien s'il n'a pas rendu d'avis.</summary>
  public int? VerdictLatencyMs { get; init; }

  /// <summary>Le temps qu'a pris le moteur lexical, ou rien s'il n'a pas rendu d'avis.</summary>
  public int? LexiconLatencyMs { get; init; }
}
