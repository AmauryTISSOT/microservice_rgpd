using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <summary>
    /// Supprime <b>tous</b> les <c>Screening</c> et toutes leurs <c>ScreenedColumn</c> : la taxonomie
    /// <c>PersonalDataCategory</c> passe aux quatorze prototypes du modèle A2 plus <c>Unflagged</c>,
    /// et aucune ligne ne doit porter une catégorie qui n'existe plus.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ⚠️ <b>Les rapports sont détruits, arbitrages compris, sans recours.</b> Relire les anciennes
    /// lignes à travers la correspondance du lexique aurait fabriqué des rapports que personne n'a
    /// produits : un <c>SpecialCategoryData</c> arbitré devenu <c>DemographicData</c> porterait un
    /// arbitrage rendu sur une autre proposition. Un historique vide se relance en connaissance de
    /// cause ; une archive réécrite se croit.
    /// </para>
    /// <para>
    /// <b>Aucune colonne ne change</b> : catégorie, degré, nom et version du moteur gardent leurs
    /// champs. Le <c>Down</c> ne rend donc rien — il n'y a pas de schéma à rendre, et les lignes
    /// n'existent plus nulle part.
    /// </para>
    /// </remarks>
    public partial class ClearScreeningsForThePrototypeTaxonomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM screened_columns;");

            migrationBuilder.Sql("DELETE FROM screenings;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
