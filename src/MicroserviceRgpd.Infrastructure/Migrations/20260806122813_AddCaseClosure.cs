using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseClosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "closing_cause",
                table: "ledger_entries",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "closed_on",
                table: "cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "closing_cause",
                table: "cases",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "closing_cause",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "closed_on",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "closing_cause",
                table: "cases");
        }
    }
}
