using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MicroserviceRgpd.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateScreenings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "screenings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    database_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    dialect = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    engine_name = table.Column<string>(type: "text", nullable: false),
                    engine_version = table.Column<string>(type: "text", nullable: false),
                    declared_column_count = table.Column<int>(type: "integer", nullable: false),
                    launched_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_screenings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "screened_columns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    table_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    column_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    data_type = table.Column<string>(type: "text", nullable: true),
                    is_nullable = table.Column<bool>(type: "boolean", nullable: true),
                    column_comment = table.Column<string>(type: "text", nullable: true),
                    table_comment = table.Column<string>(type: "text", nullable: true),
                    referenced_table = table.Column<string>(type: "text", nullable: true),
                    category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    strength = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    state = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    signed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    signed_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    screening_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_screened_columns", x => x.id);
                    table.CheckConstraint("ck_screened_columns_arbitration", "(state is null and signed_by is null and signed_on is null) or (state is not null and signed_by is not null and signed_on is not null)");
                    table.ForeignKey(
                        name: "fk_screened_columns_screenings",
                        column: x => x.screening_id,
                        principalTable: "screenings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_screened_columns_screening_id",
                table: "screened_columns",
                column: "screening_id");

            // Le triplet identifie une colonne À L'INTÉRIEUR d'un rapport : deux lignes qui le
            // partagent parlent de la même colonne, et l'un des deux arbitrages écraserait l'autre.
            // La base le tient, ce qui fait du refus « doublon » de l'ingestion un invariant de
            // schéma et non seulement une validation applicative.
            //
            // ⚠️ Il est écrit à la main parce que ses colonnes sont à cheval sur deux types du
            // modèle — la clé étrangère est sur la ligne, le triplet dans le type possédé qui
            // partage sa table — et qu'EF Core ne sait déclarer un index que sur les propriétés
            // d'un seul type. Ses colonnes de tête (screening_id, schema_name, table_name) sont
            // exactement la lecture par table de l'écran d'arbitrage : c'est pourquoi il n'y a pas
            // de second index, qui se serait payé sur chaque dépôt, cinq mille lignes à la fois.
            migrationBuilder.CreateIndex(
                name: "ux_screened_columns_identity",
                table: "screened_columns",
                columns: ["screening_id", "schema_name", "table_name", "column_name"],
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "screened_columns");

            migrationBuilder.DropTable(
                name: "screenings");
        }
    }
}
