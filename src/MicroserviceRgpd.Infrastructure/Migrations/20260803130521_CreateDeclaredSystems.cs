using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateDeclaredSystems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "declared_systems",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contents = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    adapter_address = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    declared_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    capabilities = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_declared_systems", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "declared_systems");
        }
    }
}
