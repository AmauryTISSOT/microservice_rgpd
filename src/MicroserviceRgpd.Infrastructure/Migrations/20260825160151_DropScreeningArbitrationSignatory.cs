using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <summary>
    /// Retire de l'arbitrage du <c>Screening</c> le nom saisi, et renomme sa date pour ce qu'elle
    /// est désormais seule à dire. Trois gestes, <b>une seule</b> migration : la contrainte de
    /// contrôle porte sur les colonnes des deux autres, et les séparer aurait laissé la base dans
    /// un état qu'aucune règle ne tient.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Les noms déjà enregistrés sont détruits, sans recours, y compris sur les rapports
    /// archivés.</b> Aucune autre colonne ne les porte, l'arbitrage n'a pas d'histoire, et ce
    /// contexte n'a pas d'<c>EvidenceLog</c>. <b>C'est assumé</b> — voir l'<c>ADR-0014</c>. Geler la
    /// colonne plutôt que la détruire aurait fabriqué exactement ce que le domaine dénonce : une
    /// colonne peuplée sur les vieilles lignes et vide sur les neuves, c'est-à-dire une date de
    /// déploiement déguisée en information métier.
    /// </para>
    /// <para>
    /// ⚠️ <b>Ce n'est pas un simple <c>DROP COLUMN</c>.</b> Ce qui interdisait le tiercé dépareillé
    /// est la contrainte de table, jamais la nullabilité d'une colonne prise seule : elle tombe
    /// d'abord, et se relève en duo une fois les deux colonnes restantes en place.
    /// </para>
    /// <para>
    /// ⚠️ <b>Le <c>Down</c> rend le schéma, jamais les noms</b> — ils n'existent plus nulle part. Il
    /// repose donc <c>signed_by</c> vide, et la contrainte d'alors le refusera sur toute base qui
    /// porte au moins un arbitrage : c'est le sens propre de « sans recours », et un
    /// <c>Down</c> qui aurait réussi en silence aurait menti sur ce que la migration a détruit.
    /// </para>
    /// </remarks>
    public partial class DropScreeningArbitrationSignatory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_screened_columns_arbitration",
                table: "screened_columns");

            migrationBuilder.DropColumn(
                name: "signed_by",
                table: "screened_columns");

            migrationBuilder.RenameColumn(
                name: "signed_on",
                table: "screened_columns",
                newName: "rendered_on");

            migrationBuilder.AddCheckConstraint(
                name: "ck_screened_columns_arbitration",
                table: "screened_columns",
                sql: "(state is null and rendered_on is null) or (state is not null and rendered_on is not null)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_screened_columns_arbitration",
                table: "screened_columns");

            migrationBuilder.RenameColumn(
                name: "rendered_on",
                table: "screened_columns",
                newName: "signed_on");

            migrationBuilder.AddColumn<string>(
                name: "signed_by",
                table: "screened_columns",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_screened_columns_arbitration",
                table: "screened_columns",
                sql: "(state is null and signed_by is null and signed_on is null) or (state is not null and signed_by is not null and signed_on is not null)");
        }
    }
}
