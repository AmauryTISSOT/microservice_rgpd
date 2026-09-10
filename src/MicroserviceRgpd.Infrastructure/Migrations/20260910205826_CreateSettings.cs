using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    access_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    rectification_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    erasure_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    restriction_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    portability_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    objection_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_settings", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "settings");
        }
    }
}
