using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryGesturesAndLedgerCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "covered_system_count",
                table: "ledger_entries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "declared_system_count",
                table: "ledger_entries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "delivery_declared_on",
                table: "case_claims",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "delivery_taken_on",
                table: "case_claims",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "covered_system_count",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "declared_system_count",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "delivery_declared_on",
                table: "case_claims");

            migrationBuilder.DropColumn(
                name: "delivery_taken_on",
                table: "case_claims");
        }
    }
}
