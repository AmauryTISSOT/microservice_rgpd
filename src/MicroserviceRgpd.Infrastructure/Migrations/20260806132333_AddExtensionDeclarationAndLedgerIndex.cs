using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExtensionDeclarationAndLedgerIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "informed_on",
                table: "ledger_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "extension_declared_on",
                table: "cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "extension_informed_on",
                table: "cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "extension_motive",
                table: "cases",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_case_id",
                table: "ledger_entries",
                column: "case_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ledger_entries_case_id",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "informed_on",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "extension_declared_on",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "extension_informed_on",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "extension_motive",
                table: "cases");
        }
    }
}
