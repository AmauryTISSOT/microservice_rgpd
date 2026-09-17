using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <summary>
    /// Douze colonnes de plus sur le Paramétrage, deux par droit du périmètre : l'exchange et la
    /// routing key du canal RabbitMQ (ADR-0027). Toutes nullables — un droit dont elles sont à NULL
    /// s'exerce par une adresse HTTP, ou n'est pas configuré.
    /// </summary>
    /// <remarks>
    /// Le type est `text`, SANS LONGUEUR DECLAREE, là où les colonnes d'adresse en portent une.
    /// La contrainte du domaine plafonne un ExchangeName à 255 OCTETS UTF-8 — la limite d'un
    /// shortstr AMQP —, quand une longueur en base compterait des caractères : déclarer
    /// varchar(255) ici donnerait à lire une limite qui n'est pas celle qu'on applique, et
    /// laisserait passer un nom court en caractères mais trop lourd en octets. La contrainte reste
    /// la propriété du value object, comme celles d'EndpointUrl — absolue, schéma, userinfo —, dont
    /// aucune n'est reportée en base.
    ///
    /// Aucune colonne discriminante n'est ajoutée : l'exclusivité « un droit, un seul canal » est
    /// tenue par l'agrégat, au seul endroit qui écrit, et la présence d'un exchange EST le canal
    /// RabbitMQ.
    /// </remarks>
    public partial class AddExerciseChannelRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "access_exchange",
                table: "settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "access_routing_key",
                table: "settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "erasure_exchange",
                table: "settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "erasure_routing_key",
                table: "settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "objection_exchange",
                table: "settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "objection_routing_key",
                table: "settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "portability_exchange",
                table: "settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "portability_routing_key",
                table: "settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rectification_exchange",
                table: "settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rectification_routing_key",
                table: "settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "restriction_exchange",
                table: "settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "restriction_routing_key",
                table: "settings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "access_exchange",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "access_routing_key",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "erasure_exchange",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "erasure_routing_key",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "objection_exchange",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "objection_routing_key",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "portability_exchange",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "portability_routing_key",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "rectification_exchange",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "rectification_routing_key",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "restriction_exchange",
                table: "settings");

            migrationBuilder.DropColumn(
                name: "restriction_routing_key",
                table: "settings");
        }
    }
}
