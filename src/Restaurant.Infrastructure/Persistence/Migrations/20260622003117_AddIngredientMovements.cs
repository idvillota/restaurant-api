using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIngredientMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IngredientMovementTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsInput = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientMovementTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IngredientMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientMovementTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitCostSnapshot = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientMovements_IngredientMovementTypes_IngredientMovem~",
                        column: x => x.IngredientMovementTypeId,
                        principalTable: "IngredientMovementTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientMovements_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientMovements_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientMovements_CreatedByUserId",
                table: "IngredientMovements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientMovements_IngredientId",
                table: "IngredientMovements",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientMovements_IngredientMovementTypeId",
                table: "IngredientMovements",
                column: "IngredientMovementTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientMovements_TenantId_IngredientId_OccurredAtUtc",
                table: "IngredientMovements",
                columns: new[] { "TenantId", "IngredientId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientMovements_TenantId_OccurredAtUtc",
                table: "IngredientMovements",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientMovementTypes_TenantId_Name",
                table: "IngredientMovementTypes",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.Sql("""
                INSERT INTO "IngredientMovementTypes" ("Id", "TenantId", "Name", "Description", "IsInput", "SortOrder", "IsActive", "CreatedAtUtc")
                SELECT gen_random_uuid(), t."Id", defs."Name", defs."Description", defs."IsInput", defs."SortOrder", true, NOW() AT TIME ZONE 'utc'
                FROM "Tenants" t
                CROSS JOIN (VALUES
                    ('Ingreso por regalo', 'Stock recibido sin costo de compra', true, 10),
                    ('Ajuste positivo', 'Corrección por conteo físico (más stock)', true, 20),
                    ('Salida por baja', 'Descarte intencional de producto', false, 30),
                    ('Salida por pérdida', 'Merma o deterioro no planificado', false, 40),
                    ('Ajuste negativo', 'Corrección por conteo físico (menos stock)', false, 50)
                ) AS defs("Name", "Description", "IsInput", "SortOrder")
                WHERE t."IsActive" = true;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IngredientMovements");

            migrationBuilder.DropTable(
                name: "IngredientMovementTypes");
        }
    }
}
