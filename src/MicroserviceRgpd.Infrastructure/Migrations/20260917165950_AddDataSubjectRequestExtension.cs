using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <summary>
    /// Pose sur la demande les <b>quatre colonnes de la prolongation</b> —
    /// <c>initial_response_deadline</c>, <c>extension_ground</c>, <c>extension_justification</c> et
    /// <c>extended_at</c>. <b>Quatre ajouts, aucun retrait.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Aucune valeur par défaut, aucune reprise</b> — comme l'empreinte de modification : les
    /// quatre colonnes naissent nullables et le restent, et <c>null</c> <b>est</b> la vérité des
    /// demandes existantes, qui n'ont jamais été prolongées. Les remplir inventerait une décision qui
    /// n'a pas été prise.
    /// </para>
    /// <para>
    /// ⚠️ <c>initial_response_deadline</c> <b>ne prend pas le préfixe <c>extension_</c></b> : c'est
    /// une date limite de réponse — celle qui valait avant —, pas un attribut de l'acte.
    /// <c>extended_at</c>, lui, suit <c>created_at</c> et <c>modified_at</c>.
    /// </para>
    /// <para>
    /// Le texte de la justification est en <c>text</c>, <b>sans plafond SQL</b> : le plafond de 2 000
    /// caractères est celui de l'objet valeur, qui refuse la saisie avant qu'elle n'atteigne la base.
    /// </para>
    /// </remarks>
    public partial class AddDataSubjectRequestExtension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "extended_at",
                table: "data_subject_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "extension_ground",
                table: "data_subject_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "extension_justification",
                table: "data_subject_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "initial_response_deadline",
                table: "data_subject_requests",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "extended_at",
                table: "data_subject_requests");

            migrationBuilder.DropColumn(
                name: "extension_ground",
                table: "data_subject_requests");

            migrationBuilder.DropColumn(
                name: "extension_justification",
                table: "data_subject_requests");

            migrationBuilder.DropColumn(
                name: "initial_response_deadline",
                table: "data_subject_requests");
        }
    }
}
