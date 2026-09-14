using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Infrastructure.Data.Requests;

/// <summary>
/// La table <c>execution_attempts</c> : le <b>journal d'exécution</b>, une ligne par appel parti vers
/// le système hôte (ADR-0026).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucune clé étrangère vers <c>data_subject_requests</c></b>, et c'est la décision : supprimer
/// une demande laisse ses tentatives, qui gardent un identifiant ne menant plus à personne. Une clé
/// étrangère refuserait la suppression, ou l'emporterait en cascade avec la preuve.
/// </para>
/// <para>
/// Les vocabulaires fermés sont stockés par leur nom, comme sur <c>data_subject_requests</c>. La
/// durée est un <c>interval</c>.
/// </para>
/// </remarks>
public sealed class ExecutionAttemptConfiguration : IEntityTypeConfiguration<ExecutionAttempt>
{
  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<ExecutionAttempt> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("execution_attempts");

    // La clé est forgée par le service en GUID v7 ; la base ne la génère jamais.
    builder.HasKey(attempt => attempt.Id).HasName("pk_execution_attempts");
    builder.Property(attempt => attempt.Id)
      .HasColumnName("id")
      .HasConversion(id => id.Value, value => ExecutionAttemptId.From(value))
      .ValueGeneratedNever();

    builder.Property(attempt => attempt.DataSubjectRequestId)
      .HasColumnName("data_subject_request_id")
      .HasConversion(id => id.Value, value => DataSubjectRequestId.From(value))
      .IsRequired();

    builder.Property(attempt => attempt.Right)
      .HasColumnName("data_subject_right")
      .HasConversion(right => right.Name, name => DataSubjectRight.FromName(name))
      .IsRequired();

    builder.Property(attempt => attempt.CalledUrl).HasColumnName("called_url").IsRequired();

    builder.Property(attempt => attempt.StartedAt).HasColumnName("started_at").IsRequired();

    builder.Property(attempt => attempt.Duration).HasColumnName("duration").IsRequired();

    builder.Property(attempt => attempt.Outcome)
      .HasColumnName("outcome")
      .HasConversion(outcome => outcome.Name, name => ExecutionOutcome.FromName(name))
      .IsRequired();

    // Le nul dit « le système hôte n'a pas répondu » : délai dépassé ou erreur réseau.
    builder.Property(attempt => attempt.HttpStatus).HasColumnName("http_status");

    builder.Property(attempt => attempt.CreatedBy).HasColumnName("created_by").IsRequired();
  }
}
