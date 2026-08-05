using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimOriginAndIdentityMotivation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "identity_verification_method",
                table: "ledger_entries",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "received_on",
                table: "ledger_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identity_motivation_detail",
                table: "cases",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "identity_verification_method",
                table: "cases",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            // Vrai pour les lignes déjà écrites, et c'est un fait plutôt qu'un repli : seul le canal
            // de l'application a pu les écrire, il ne connaît que « Named », et rien n'y était donc
            // resté à confirmer. Le faux aurait fait naître rétroactivement des confirmations dues
            // sur des droits que personne n'a jamais proposés.
            migrationBuilder.AddColumn<bool>(
                name: "confirmed",
                table: "case_claims",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // « ApplicationSession » pour les lignes déjà écrites, et non la chaîne vide qu'EF
            // proposerait : seul le canal de l'application a pu les écrire, et il pose cette valeur
            // lui-même. Une chaîne vide serait une déclaration hors du vocabulaire fermé, que la
            // relecture refuserait — le dossier deviendrait illisible plutôt que faux.
            migrationBuilder.AddColumn<string>(
                name: "identity_at_origin",
                table: "case_claims",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "ApplicationSession");

            // « Named » pour la même raison : la personne a coché ses droits dans l'application.
            migrationBuilder.AddColumn<string>(
                name: "origin",
                table: "case_claims",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Named");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "identity_verification_method",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "received_on",
                table: "ledger_entries");

            migrationBuilder.DropColumn(
                name: "identity_motivation_detail",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "identity_verification_method",
                table: "cases");

            migrationBuilder.DropColumn(
                name: "confirmed",
                table: "case_claims");

            migrationBuilder.DropColumn(
                name: "identity_at_origin",
                table: "case_claims");

            migrationBuilder.DropColumn(
                name: "origin",
                table: "case_claims");
        }
    }
}
