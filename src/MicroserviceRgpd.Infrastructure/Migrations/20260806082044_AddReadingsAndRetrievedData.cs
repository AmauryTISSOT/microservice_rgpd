using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReadingsAndRetrievedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "case_readings",
                columns: table => new
                {
                    data_subject_right = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    declared_system_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    asked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    declared_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    designations_at_call = table.Column<int>(type: "integer", nullable: false)
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
                name: "case_retrieved_data",
                columns: table => new
                {
                    case_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_subject_right = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    declared_system_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    content_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    retrieved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_case_retrieved_data", x => new { x.case_id, x.data_subject_right, x.declared_system_id });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "case_readings");

            migrationBuilder.DropTable(
                name: "case_retrieved_data");
        }
    }
}
