using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <summary>
    /// Pose sur la demande son statut, <c>status</c> (ADR-0021), stocké par le nom de la valeur.
    /// <b>Un ajout, aucun retrait.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Les demandes existantes sont reprises <c>InProgress</c>, et c'est vrai par
    /// construction</b> : aucun <c>Gesture</c> n'existait qui les aurait terminées ou annulées.
    /// </para>
    /// <para>
    /// ⚠️ <b>Le remplissage se fait en trois temps, et la colonne ne garde AUCUNE valeur par
    /// défaut</b>, comme <c>listing_origin</c> : seule <c>DataSubjectRequest.Receive</c> fixe le
    /// statut de naissance. La colonne naît donc nullable, se remplit une fois, puis se ferme.
    /// </para>
    /// </remarks>
    public partial class AddDataSubjectRequestStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "data_subject_requests",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(
                "update data_subject_requests set status = 'InProgress' where status is null;");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "data_subject_requests",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "status",
                table: "data_subject_requests");
        }
    }
}
