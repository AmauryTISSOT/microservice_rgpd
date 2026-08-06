using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLocatingsReservationsAndOpenQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "declared_deadline",
                table: "ledger_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "case_locatings",
                columns: table => new
                {
                    declared_system_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    asked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    declared_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    designations_at_call = table.Column<int>(type: "integer", nullable: false)
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
                    subject = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    declared_system_id = table.Column<string>(type: "character varying(64)", nullable: false),
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "case_locating_references");

            migrationBuilder.DropTable(
                name: "case_questions");

            migrationBuilder.DropTable(
                name: "case_reservation_designations");

            migrationBuilder.DropTable(
                name: "case_reservations");

            migrationBuilder.DropTable(
                name: "case_locatings");

            migrationBuilder.DropColumn(
                name: "declared_deadline",
                table: "ledger_entries");
        }
    }
}
