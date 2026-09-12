using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <summary>
    /// Pose sur la demande son empreinte de modification, <c>modified_by</c> et <c>modified_at</c>.
    /// <b>Deux ajouts, aucun retrait.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Aucune reprise, et pas de remplissage « en trois temps »</b> — contrairement au statut
    /// et à la date limite de réponse. Les deux colonnes naissent nullables et le restent : <c>null</c>
    /// <b>est</b> la vérité des lignes existantes, qui n'ont jamais été modifiées. Les remplir
    /// inventerait une correction qui n'a pas eu lieu.
    /// </para>
    /// <para>
    /// Rien n'écrit encore ces colonnes : le geste de modification vient ensuite.
    /// </para>
    /// </remarks>
    public partial class AddDataSubjectRequestModification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "modified_at",
                table: "data_subject_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "modified_by",
                table: "data_subject_requests",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "modified_at",
                table: "data_subject_requests");

            migrationBuilder.DropColumn(
                name: "modified_by",
                table: "data_subject_requests");
        }
    }
}
