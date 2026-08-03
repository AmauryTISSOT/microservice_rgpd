using MicroserviceRgpd.Core.Casework.Ledger;

namespace MicroserviceRgpd.Infrastructure.Data.Casework;

/// <summary>
/// La table du <c>Ledger</c> : sept colonnes, et pas une de plus où un nom pourrait entrer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aucune clé étrangère vers <c>cases</c>.</b> Ce n'est pas un oubli : le <c>Ledger</c> survit
/// au dossier de cinq ans, et une contrainte référentielle rendrait la destruction du dossier
/// impossible — ou, pire, emporterait la preuve avec lui. Le <c>case_id</c> est une référence
/// <b>libre</b>, et il le restera.
/// </para>
/// <para>
/// <b>Aucun index secondaire pour l'instant.</b> Rien ne lit encore cette table, et un index
/// coûterait à chaque écriture. Le jour où l'écran de la file lira les <c>Ledger</c> échus, ce sera
/// un geste délibéré, avec sa migration.
/// </para>
/// <para>
/// <b>Le <c>snake_case</c> est déclaré ici, explicitement</b>, comme sur les autres tables du dépôt.
/// </para>
/// </remarks>
public sealed class LedgerRowConfiguration : IEntityTypeConfiguration<LedgerRow>
{
  /// <summary>
  /// Le plafond d'une colonne portant le mot d'un <b>vocabulaire fermé</b> — un nom de membre,
  /// jamais un texte. Il dit à la base ce qu'elle attend, et la fait cesser d'accepter ce que le
  /// domaine n'écrira jamais.
  /// </summary>
  private const int ClosedVocabularyLength = 64;

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<LedgerRow> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("ledger_entries");

    // La clé est l'identité de la ligne, forgée en GUID v7 : ordonnée dans le temps, donc sans
    // fragmentation d'index à l'insertion, et sans qu'aucune ligne n'ait eu à en compter une autre.
    builder.HasKey(row => row.EntryId).HasName("pk_ledger_entries");
    builder.Property(row => row.EntryId).HasColumnName("entry_id").ValueGeneratedNever();

    builder.Property(row => row.CaseId).HasColumnName("case_id").IsRequired();

    builder.Property(row => row.OccurredAt).HasColumnName("occurred_at").IsRequired();

    builder.Property(row => row.Fact)
      .HasColumnName("fact")
      .HasMaxLength(ClosedVocabularyLength)
      .IsRequired();

    builder.Property(row => row.SignatoryKind)
      .HasColumnName("signatory_kind")
      .HasMaxLength(ClosedVocabularyLength)
      .IsRequired();

    // Le plafond du nom vit une seule fois, là où le domaine le déclare : deux constantes valant
    // chacune 200 finiraient par ne plus valoir la même chose.
    builder.Property(row => row.SignatoryName)
      .HasColumnName("signatory_name")
      .HasMaxLength(Signatory.MaxNameLength);

    builder.Property(row => row.IdentityDeclaration)
      .HasColumnName("identity_declaration")
      .HasMaxLength(ClosedVocabularyLength);

    builder.Property(row => row.DesignationCount).HasColumnName("designation_count");
  }
}
