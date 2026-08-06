using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Ledger;

namespace MicroserviceRgpd.Infrastructure.Data.Casework;

/// <summary>
/// La table du <c>Ledger</c> : seize colonnes, et pas une de plus où un nom de personne concernée
/// pourrait entrer. La seule prose est celle de <b>preuve</b> ; la prose de travail, qui nomme par
/// nature, n'a aucune colonne ici.
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
      .HasMaxLength(CaseworkSchema.ClosedVocabularyLength)
      .IsRequired();

    builder.Property(row => row.SignatoryKind)
      .HasColumnName("signatory_kind")
      .HasMaxLength(CaseworkSchema.ClosedVocabularyLength)
      .IsRequired();

    // Le plafond du nom vit une seule fois, là où le domaine le déclare : deux constantes valant
    // chacune 200 finiraient par ne plus valoir la même chose.
    builder.Property(row => row.SignatoryName)
      .HasColumnName("signatory_name")
      .HasMaxLength(Signatory.MaxNameLength);

    builder.Property(row => row.IdentityDeclaration)
      .HasColumnName("identity_declaration")
      .HasMaxLength(CaseworkSchema.ClosedVocabularyLength);

    builder.Property(row => row.DesignationCount).HasColumnName("designation_count");

    // Le plafond de l'identifiant vit une seule fois, là où le domaine le déclare : c'est le même
    // identifiant que celui de la table des systèmes déclarés, et deux constantes valant chacune 64
    // finiraient par ne plus valoir la même chose.
    builder.Property(row => row.DeclaredSystem)
      .HasColumnName("declared_system")
      .HasMaxLength(DeclaredSystemId.MaxLength);

    builder.Property(row => row.SignatureRegime)
      .HasColumnName("signature_regime")
      .HasMaxLength(CaseworkSchema.ClosedVocabularyLength);

    builder.Property(row => row.DataSubjectRight)
      .HasColumnName("data_subject_right")
      .HasMaxLength(CaseworkSchema.ClosedVocabularyLength);

    builder.Property(row => row.StepState)
      .HasColumnName("step_state")
      .HasMaxLength(CaseworkSchema.ClosedVocabularyLength);

    // Le plafond de la prose vit une seule fois, là où le domaine le déclare. La colonne est bornée
    // plutôt que libre : ce qui descend ici survit cinq ans à la clôture, et une colonne sans
    // plafond serait la seule de la table à ne pas dire ce qu'elle accepte.
    builder.Property(row => row.EvidenceProse)
      .HasColumnName("evidence_prose")
      .HasMaxLength(LedgerEntry.MaxEvidenceProseLength);

    builder.Property(row => row.ReceptionWasDefaulted).HasColumnName("reception_was_defaulted");

    // Une colonne distincte d'`occurred_at`, qui date le geste : le dépôt manuel transcrit un
    // courriel reçu il y a un nombre de jours inconnu, et une seule date aurait fait choisir entre
    // dire depuis quand la personne attend et dire depuis quand le service savait.
    builder.Property(row => row.ReceivedOn).HasColumnName("received_on");

    // La moitié de la motivation qui se compte, et la seule qui entre ici. ⚠️ Il n'existe aucune
    // colonne pour le détail en prose : il est nominatif par nature, et cette table survit cinq ans
    // à la clôture du dossier.
    builder.Property(row => row.IdentityVerificationMethod)
      .HasColumnName("identity_verification_method")
      .HasMaxLength(CaseworkSchema.ClosedVocabularyLength);

    // L'échéance d'un différé, déclarée par le client. Aucune colonne ne compte les passages : une
    // relance n'a aucun signataire, et son compte serait du bruit de mécanique dans la preuve.
    builder.Property(row => row.DeclaredDeadline).HasColumnName("declared_deadline");
  }
}
