using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <summary>
    /// La colonne du journal d'exécution qui portait l'URL appelée devient l'<b>exercice</b> : par où
    /// la remise est partie — une adresse, ou un exchange et une routing key en toutes lettres
    /// (ADR-0028).
    /// </summary>
    /// <remarks>
    /// Elle RENOMME, elle ne recrée pas : les lignes existantes gardent leur valeur, une adresse
    /// étant déjà un exercice. Aucune colonne n'est ajoutée, et AUCUNE COLONNE DISCRIMINANTE ne redit
    /// le canal — même motif qu'à l'ADR-0027, où la présence d'un exchange EST le canal RabbitMQ.
    /// </remarks>
    public partial class RenameCalledUrlToExercise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "called_url",
                table: "execution_attempts",
                newName: "exercise");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "exercise",
                table: "execution_attempts",
                newName: "called_url");
        }
    }
}
