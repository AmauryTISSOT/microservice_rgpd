using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.UseCases.Qualifications.Qualify;

namespace MicroserviceRgpd.Infrastructure.Data.Audit;

/// <summary>
/// La seule table du dépôt, et le seul endroit où son nommage est décidé.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le <c>snake_case</c> est déclaré ici, explicitement, sans paquet de conventions.</b> Pour une
/// seule table, un fichier local et vérifiable vaut mieux qu'une dépendance dont la compatibilité
/// avec cette version d'EF Core n'est pas établie — et qui déciderait en silence du nom de toutes
/// les tables à venir.
/// </para>
/// <para>
/// <b>Aucun index secondaire.</b> Rien ne lit cette table, aucune purge n'est prévue, et un index
/// sur l'horodatage coûterait à chaque écriture — c'est-à-dire sur le chemin synchrone de chaque
/// qualification, pour un gain que personne ne réclame.
/// </para>
/// </remarks>
public sealed class QualificationAuditRowConfiguration : IEntityTypeConfiguration<QualificationAuditRow>
{
  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<QualificationAuditRow> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("qualification_audit_entries");

    // La clé est celle que le service a rendue à l'appelant, forgée en GUID v7 : ordonnée dans le
    // temps, donc sans fragmentation d'index à l'insertion. La base ne la génère jamais.
    // La contrainte est nommée elle aussi : le nommage par défaut d'EF Core introduirait dans la
    // base un second style d'écriture, que la table n'emploie nulle part ailleurs.
    builder.HasKey(row => row.QualificationId).HasName("pk_qualification_audit_entries");
    builder.Property(row => row.QualificationId).HasColumnName("qualification_id").ValueGeneratedNever();

    builder.Property(row => row.OccurredAt).HasColumnName("occurred_at").IsRequired();

    // Miroir du plafond public : la base cesse d'accepter ce que le contrat interdit. En PostgreSQL
    // le plafond ne coûte rien de plus qu'un `text` — au prix d'une migration si le plafond bouge.
    builder.Property(row => row.Text)
      .HasColumnName("text")
      .HasMaxLength(RightsRequestText.MaxLength)
      .IsRequired();

    // Les trois ensembles de droits portent les noms canoniques anglais de la taxonomie : la base
    // n'introduit aucun troisième vocabulaire. Table fille écartée — surdimensionnée pour un non
    // agrégat —, `jsonb` écarté — moins typé, sans gain —, chaîne jointe écartée — toute requête
    // deviendrait textuelle.
    builder.Property(row => row.Rights).HasColumnName("rights").IsRequired();

    // Par leur nom, jamais par un entier : un entier rendrait la table illisible et la lierait à
    // l'ordre de déclaration d'un `enum` que personne ne pense à tenir stable.
    builder.Property(row => row.ReviewSignal).HasColumnName("review_signal").IsRequired();

    builder.Property(row => row.VerdictRights).HasColumnName("verdict_rights");
    builder.Property(row => row.VerdictDeclaredConfidence).HasColumnName("verdict_declared_confidence");
    builder.Property(row => row.VerdictEngineName).HasColumnName("verdict_engine_name");
    builder.Property(row => row.VerdictEngineVersion).HasColumnName("verdict_engine_version");

    builder.Property(row => row.WitnessRights).HasColumnName("witness_rights");
    builder.Property(row => row.WitnessEngineName).HasColumnName("witness_engine_name");
    builder.Property(row => row.WitnessEngineVersion).HasColumnName("witness_engine_version");

    builder.Property(row => row.Justification).HasColumnName("justification");

    // Le plafond de la référence appelante vit une seule fois, là où le contrat le déclare : deux
    // constantes valant chacune 64 finiraient par ne plus valoir la même chose, et la base cesserait
    // alors d'accepter ce que le contrat promet — ou l'inverse.
    builder.Property(row => row.CallerReference)
      .HasColumnName("caller_reference")
      .HasMaxLength(QualifyCommand.MaxCallerReferenceLength);

    // Un `traceparent` W3C tient largement dans cette borne ; elle est là pour que la colonne dise
    // qu'elle attend un identifiant, non un texte libre.
    builder.Property(row => row.TraceId).HasColumnName("trace_id").HasMaxLength(64);

    builder.Property(row => row.TotalLatencyMs).HasColumnName("total_latency_ms").IsRequired();
    builder.Property(row => row.VerdictLatencyMs).HasColumnName("verdict_latency_ms");
    builder.Property(row => row.WitnessLatencyMs).HasColumnName("witness_latency_ms");
  }
}
