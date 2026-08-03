using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateCasesAndLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    identity_declaration = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    received_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cases", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                columns: table => new
                {
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fact = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    signatory_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    signatory_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    identity_declaration = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    designation_count = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger_entries", x => x.entry_id);
                });

            migrationBuilder.CreateTable(
                name: "case_claims",
                columns: table => new
                {
                    data_subject_right = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_claims", x => new { x.case_id, x.data_subject_right });
                    table.ForeignKey(
                        name: "fk_case_claims_cases",
                        column: x => x.case_id,
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "case_designations",
                columns: table => new
                {
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    value = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_designations", x => new { x.case_id, x.ordinal });
                    table.ForeignKey(
                        name: "fk_case_designations_cases",
                        column: x => x.case_id,
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "case_steps",
                columns: table => new
                {
                    declared_system_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_subject_right = table.Column<string>(type: "character varying(64)", nullable: false),
                    state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_steps", x => new { x.case_id, x.data_subject_right, x.declared_system_id });
                    table.ForeignKey(
                        name: "fk_case_steps_case_claims",
                        columns: x => new { x.case_id, x.data_subject_right },
                        principalTable: "case_claims",
                        principalColumns: new[] { "case_id", "data_subject_right" },
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "case_designations");

            migrationBuilder.DropTable(
                name: "case_steps");

            migrationBuilder.DropTable(
                name: "ledger_entries");

            migrationBuilder.DropTable(
                name: "case_claims");

            migrationBuilder.DropTable(
                name: "cases");
        }
    }
}
