using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseStateAndReceptionRegime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "data_subject_right",
                table: "ledger_entries",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "evidence_prose",
                table: "ledger_entries",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "reception_was_defaulted",
                table: "ledger_entries",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "signature_regime",
                table: "ledger_entries",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "step_state",
                table: "ledger_entries",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // Faux pour les lignes déjà écrites, et c'est un fait plutôt qu'un repli : seul le canal
            // de l'application a pu les écrire, et il déclare toujours la date de réception — c'est le
            // dépôt manuel qui aura besoin du défaut.
            migrationBuilder.AddColumn<bool>(
                name: "reception_is_default",
                table: "cases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // « Open » pour les lignes déjà écrites, et non la chaîne vide qu'EF proposerait : aucune
            // clôture n'existe encore, donc tout dossier écrit avant cette migration est ouvert. Une
            // chaîne vide serait un état hors du vocabulaire fermé, que la relecture refuserait.
            migrationBuilder.AddColumn<string>(
                name: "state",
                table: "cases",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Open");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "data_subject_right",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "evidence_prose",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "reception_was_defaulted",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "signature_regime",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "step_state",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "reception_is_default",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "state",
                table: "cases");
        }
    }
}
