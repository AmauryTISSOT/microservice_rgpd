using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <summary>
    /// Pose sur le rapport l'<c>origine du relevé</c> — collé, ou scanné — et sur la colonne
    /// détectée la <c>raison d'absence d'aperçu</c>. <b>Deux ajouts, aucun retrait</b>, et une seule
    /// migration : les deux colonnes sont les deux moitiés d'un même vocabulaire neuf, et une base
    /// qui porterait l'une sans l'autre ne saurait rendre ni la clause d'incomplétude ni ses
    /// comptes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Les rapports existants sont relus en <c>Collé</c>, et c'est vrai par construction</b> :
    /// aucun chemin connecté n'existait quand ils ont été produits. Aucun rapport ancien ne se met
    /// donc à mentir, et sa clause dit après ce qu'elle disait avant.
    /// </para>
    /// <para>
    /// ⚠️ <b>Le remplissage se fait en trois gestes, et la colonne ne garde AUCUNE valeur par
    /// défaut.</b> Un <c>AddColumn</c> non nullable à valeur par défaut aurait laissé dans la base un
    /// <c>DEFAULT 'Pasted'</c> permanent, c'est-à-dire la porte de derrière que le cas nul de
    /// <c>ListingOrigin</c> existe pour fermer : un chemin d'écriture neuf qui oublierait l'origine
    /// obtiendrait « collé » en silence, sur un rapport scanné. La colonne naît donc nullable, se
    /// remplit une fois, puis se ferme.
    /// </para>
    /// <para>
    /// <b>La raison d'absence d'aperçu reste nulle sur ces lignes</b>, et ce n'est pas un trou : ces
    /// rapports n'ont jamais eu d'aperçu, et les quatre comptes de la clause sont <b>absents</b> sur
    /// le chemin collé, jamais à zéro. « Zéro colonne sans aperçu » se lirait comme un prélèvement
    /// qui a tout réussi.
    /// </para>
    /// <para>
    /// ⚠️ <b>Le <c>Down</c> rend le schéma, jamais les origines</b> — la colonne en est le seul
    /// porteur, et rien ailleurs ne dit d'un rapport qu'il a été scanné. Rejouer le <c>Up</c> après
    /// lui relirait <b>tous</b> les rapports en « collé », y compris ceux qui ne l'étaient pas :
    /// c'est le sens propre de « aucun retrait », et cela vaut d'être écrit ici plutôt que découvert
    /// un jour de bascule.
    /// </para>
    /// </remarks>
    public partial class AddScreeningListingOriginAndPreviewAbsenceReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "listing_origin",
                table: "screenings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // Le sort des rapports existants, écrit en toutes lettres : ils ont tous été produits
            // par un collage, puisque c'était le seul chemin qui existait.
            migrationBuilder.Sql(
                "update screenings set listing_origin = 'Pasted' where listing_origin is null;");

            migrationBuilder.AlterColumn<string>(
                name: "listing_origin",
                table: "screenings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preview_absence_reason",
                table: "screened_columns",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "listing_origin",
                table: "screenings");

            migrationBuilder.DropColumn(
                name: "preview_absence_reason",
                table: "screened_columns");
        }
    }
}
