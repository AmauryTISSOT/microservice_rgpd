using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateDataSubjectRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "data_subject_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin = table.Column<string>(type: "text", nullable: false),
                    received_on = table.Column<DateOnly>(type: "date", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: true),
                    first_name = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    identity_verified = table.Column<bool>(type: "boolean", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    data_subject_right = table.Column<string>(type: "text", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_subject_requests", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_subject_requests");
        }
    }
}
