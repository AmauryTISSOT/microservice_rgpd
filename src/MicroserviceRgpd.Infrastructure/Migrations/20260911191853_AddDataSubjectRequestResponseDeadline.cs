using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <summary>
    /// Pose sur la demande sa date limite de réponse, <c>response_deadline</c> (ADR-0021).
    /// <b>Un ajout, aucun retrait.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Les demandes existantes sont reprises depuis leur date de réception</b> :
    /// <c>received_on + interval '1 month'</c>, que PostgreSQL ramène au dernier jour du mois suivant
    /// quand ce jour n'y existe pas — à l'identique de <c>DateOnly.AddMonths</c>, que
    /// <c>DataSubjectRequest.Receive</c> applique aux demandes nouvelles.
    /// </para>
    /// <para>
    /// ⚠️ <b>Le remplissage se fait en trois temps, et la colonne ne garde AUCUNE valeur par
    /// défaut</b>, comme <c>status</c> : seule <c>DataSubjectRequest.Receive</c> fixe la date limite.
    /// La colonne naît donc nullable, se remplit une fois, puis se ferme.
    /// </para>
    /// </remarks>
    public partial class AddDataSubjectRequestResponseDeadline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "response_deadline",
                table: "data_subject_requests",
                type: "date",
                nullable: true);

            migrationBuilder.Sql(
                "update data_subject_requests set response_deadline = (received_on + interval '1 month')::date where response_deadline is null;");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "response_deadline",
                table: "data_subject_requests",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "response_deadline",
                table: "data_subject_requests");
        }
    }
}
