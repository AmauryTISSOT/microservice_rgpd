using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateQualificationAuditEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "qualification_audit_entries",
                columns: table => new
                {
                    qualification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    text = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    rights = table.Column<string[]>(type: "text[]", nullable: false),
                    review_signal = table.Column<string>(type: "text", nullable: false),
                    verdict_rights = table.Column<string[]>(type: "text[]", nullable: true),
                    verdict_declared_confidence = table.Column<string>(type: "text", nullable: true),
                    verdict_engine_name = table.Column<string>(type: "text", nullable: true),
                    verdict_engine_version = table.Column<string>(type: "text", nullable: true),
                    witness_rights = table.Column<string[]>(type: "text[]", nullable: true),
                    witness_engine_name = table.Column<string>(type: "text", nullable: true),
                    witness_engine_version = table.Column<string>(type: "text", nullable: true),
                    justification = table.Column<string>(type: "text", nullable: true),
                    caller_reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    trace_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    total_latency_ms = table.Column<int>(type: "integer", nullable: false),
                    verdict_latency_ms = table.Column<int>(type: "integer", nullable: true),
                    witness_latency_ms = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_qualification_audit_entries", x => x.qualification_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "qualification_audit_entries");
        }
    }
}
