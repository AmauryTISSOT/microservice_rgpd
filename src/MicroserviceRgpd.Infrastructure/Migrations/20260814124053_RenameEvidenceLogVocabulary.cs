using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <summary>
    /// Renomme le vocabulaire de la preuve et de l'avis du lexique. Elle <b>renomme</b>, elle ne
    /// recrée pas : aucune ligne existante n'est perdue.
    /// </summary>
    /// <remarks>
    /// L'échafaudage d'EF proposait un <c>DropTable</c> suivi d'un <c>CreateTable</c>, parce que le
    /// type CLR de la ligne a changé de nom en même temps que la table. C'aurait détruit la preuve
    /// de chaque <c>Case</c> déjà ouvert. Les gestes ci-dessous sont écrits à la main pour cette
    /// raison, et le <c>Down</c> les défait un à un.
    /// </remarks>
    public partial class RenameEvidenceLogVocabulary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "ledger_entries",
                newName: "evidence_log_entries");

            migrationBuilder.Sql(
                "ALTER TABLE evidence_log_entries RENAME CONSTRAINT pk_ledger_entries TO pk_evidence_log_entries;");

            migrationBuilder.RenameIndex(
                name: "ix_ledger_entries_case_id",
                table: "evidence_log_entries",
                newName: "ix_evidence_log_entries_case_id");

            migrationBuilder.RenameColumn(
                name: "signature_regime",
                table: "evidence_log_entries",
                newName: "signer_verification");

            migrationBuilder.RenameColumn(
                name: "witness_rights",
                table: "qualification_audit_entries",
                newName: "lexicon_rights");

            migrationBuilder.RenameColumn(
                name: "witness_declared_confidence",
                table: "qualification_audit_entries",
                newName: "lexicon_declared_confidence");

            migrationBuilder.RenameColumn(
                name: "witness_engine_name",
                table: "qualification_audit_entries",
                newName: "lexicon_engine_name");

            migrationBuilder.RenameColumn(
                name: "witness_engine_version",
                table: "qualification_audit_entries",
                newName: "lexicon_engine_version");

            migrationBuilder.RenameColumn(
                name: "witness_latency_ms",
                table: "qualification_audit_entries",
                newName: "lexicon_latency_ms");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "lexicon_latency_ms",
                table: "qualification_audit_entries",
                newName: "witness_latency_ms");

            migrationBuilder.RenameColumn(
                name: "lexicon_engine_version",
                table: "qualification_audit_entries",
                newName: "witness_engine_version");

            migrationBuilder.RenameColumn(
                name: "lexicon_engine_name",
                table: "qualification_audit_entries",
                newName: "witness_engine_name");

            migrationBuilder.RenameColumn(
                name: "lexicon_declared_confidence",
                table: "qualification_audit_entries",
                newName: "witness_declared_confidence");

            migrationBuilder.RenameColumn(
                name: "lexicon_rights",
                table: "qualification_audit_entries",
                newName: "witness_rights");

            migrationBuilder.RenameColumn(
                name: "signer_verification",
                table: "evidence_log_entries",
                newName: "signature_regime");

            migrationBuilder.RenameIndex(
                name: "ix_evidence_log_entries_case_id",
                table: "evidence_log_entries",
                newName: "ix_ledger_entries_case_id");

            migrationBuilder.Sql(
                "ALTER TABLE evidence_log_entries RENAME CONSTRAINT pk_evidence_log_entries TO pk_ledger_entries;");

            migrationBuilder.RenameTable(
                name: "evidence_log_entries",
                newName: "ledger_entries");
        }
    }
}
