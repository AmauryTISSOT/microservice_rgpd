using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropCasework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "case_designations");

            migrationBuilder.DropTable(
                name: "case_locating_references");

            migrationBuilder.DropTable(
                name: "case_questions");

            migrationBuilder.DropTable(
                name: "case_readings");

            migrationBuilder.DropTable(
                name: "case_reservation_designations");

            migrationBuilder.DropTable(
                name: "case_retrieved_data");

            migrationBuilder.DropTable(
                name: "case_steps");

            migrationBuilder.DropTable(
                name: "declared_systems");

            migrationBuilder.DropTable(
                name: "evidence_log_entries");

            migrationBuilder.DropTable(
                name: "case_reservations");

            migrationBuilder.DropTable(
                name: "case_claims");

            migrationBuilder.DropTable(
                name: "case_locatings");

            migrationBuilder.DropTable(
                name: "cases");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "case_retrieved_data",
                columns: table => new
                {
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_subject_right = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    declared_system_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    retrieved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    content_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_retrieved_data", x => new { x.case_id, x.data_subject_right, x.declared_system_id });
                });

            migrationBuilder.CreateTable(
                name: "cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    closed_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closing_cause = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    identity_declaration = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    extension_declared_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    extension_informed_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    extension_motive = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    identity_motivation_detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    identity_verification_method = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    reception_is_default = table.Column<bool>(type: "boolean", nullable: false),
                    received_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cases", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "declared_systems",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    adapter_address = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    contents = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    declared_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    capabilities = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_declared_systems", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "evidence_log_entries",
                columns: table => new
                {
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    closing_cause = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    covered_system_count = table.Column<int>(type: "integer", nullable: true),
                    data_subject_right = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    declared_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    declared_system = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    declared_system_count = table.Column<int>(type: "integer", nullable: true),
                    designation_count = table.Column<int>(type: "integer", nullable: true),
                    fact = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    identity_declaration = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    identity_verification_method = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    informed_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    evidence_prose = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    received_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reception_was_defaulted = table.Column<bool>(type: "boolean", nullable: true),
                    signatory_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    signatory_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    signer_verification = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    step_state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evidence_log_entries", x => x.entry_id);
                });

            migrationBuilder.CreateTable(
                name: "case_claims",
                columns: table => new
                {
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_subject_right = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    delivery_declared_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    delivery_taken_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    identity_at_origin = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    origin = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                name: "case_locatings",
                columns: table => new
                {
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    declared_system_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    asked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    declared_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    designations_at_call = table.Column<int>(type: "integer", nullable: false),
                    last_outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_locatings", x => new { x.case_id, x.declared_system_id });
                    table.ForeignKey(
                        name: "fk_case_locatings_cases",
                        column: x => x.case_id,
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "case_questions",
                columns: table => new
                {
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    asked_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_questions", x => new { x.case_id, x.subject });
                    table.ForeignKey(
                        name: "fk_case_questions_cases",
                        column: x => x.case_id,
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "case_readings",
                columns: table => new
                {
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_subject_right = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    declared_system_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    asked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    declared_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    designations_at_call = table.Column<int>(type: "integer", nullable: false),
                    last_outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_readings", x => new { x.case_id, x.data_subject_right, x.declared_system_id });
                    table.ForeignKey(
                        name: "fk_case_readings_cases",
                        column: x => x.case_id,
                        principalTable: "cases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "case_steps",
                columns: table => new
                {
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_subject_right = table.Column<string>(type: "character varying(64)", nullable: false),
                    declared_system_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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

            migrationBuilder.CreateTable(
                name: "case_locating_references",
                columns: table => new
                {
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    declared_system_id = table.Column<string>(type: "character varying(64)", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_locating_references", x => new { x.case_id, x.declared_system_id, x.ordinal });
                    table.ForeignKey(
                        name: "fk_case_locating_references_case_locatings",
                        columns: x => new { x.case_id, x.declared_system_id },
                        principalTable: "case_locatings",
                        principalColumns: new[] { "case_id", "declared_system_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "case_reservations",
                columns: table => new
                {
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    declared_system_id = table.Column<string>(type: "character varying(64)", nullable: false),
                    reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_reservations", x => new { x.case_id, x.declared_system_id, x.reference });
                    table.ForeignKey(
                        name: "fk_case_reservations_case_locatings",
                        columns: x => new { x.case_id, x.declared_system_id },
                        principalTable: "case_locatings",
                        principalColumns: new[] { "case_id", "declared_system_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "case_reservation_designations",
                columns: table => new
                {
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    declared_system_id = table.Column<string>(type: "character varying(64)", nullable: false),
                    reference = table.Column<string>(type: "character varying(200)", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    value = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_reservation_designations", x => new { x.case_id, x.declared_system_id, x.reference, x.ordinal });
                    table.ForeignKey(
                        name: "fk_case_reservation_designations_case_reservations",
                        columns: x => new { x.case_id, x.declared_system_id, x.reference },
                        principalTable: "case_reservations",
                        principalColumns: new[] { "case_id", "declared_system_id", "reference" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_evidence_log_entries_case_id",
                table: "evidence_log_entries",
                column: "case_id");
        }
    }
}
