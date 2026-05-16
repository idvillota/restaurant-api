using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddIngredientCategories : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "IngredientCategories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "text", nullable: true),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IngredientCategories", x => x.Id);
            });

        migrationBuilder.AddColumn<Guid>(
            name: "IngredientCategoryId",
            table: "Ingredients",
            type: "uuid",
            nullable: true);

        migrationBuilder.Sql("""
            INSERT INTO "IngredientCategories" ("Id", "TenantId", "Name", "Description", "SortOrder", "IsActive", "CreatedAtUtc")
            SELECT gen_random_uuid(), sub."TenantId", 'Uncategorized', NULL, 0, true, NOW() AT TIME ZONE 'utc'
            FROM (SELECT DISTINCT "TenantId" FROM "Ingredients") sub;

            UPDATE "Ingredients" i
            SET "IngredientCategoryId" = c."Id"
            FROM "IngredientCategories" c
            WHERE c."TenantId" = i."TenantId" AND c."Name" = 'Uncategorized';
            """);

        migrationBuilder.Sql("""
            ALTER TABLE "Ingredients" ALTER COLUMN "IngredientCategoryId" SET NOT NULL;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_Ingredients_IngredientCategoryId",
            table: "Ingredients",
            column: "IngredientCategoryId");

        migrationBuilder.AddForeignKey(
            name: "FK_Ingredients_IngredientCategories_IngredientCategoryId",
            table: "Ingredients",
            column: "IngredientCategoryId",
            principalTable: "IngredientCategories",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Ingredients_IngredientCategories_IngredientCategoryId",
            table: "Ingredients");

        migrationBuilder.DropIndex(
            name: "IX_Ingredients_IngredientCategoryId",
            table: "Ingredients");

        migrationBuilder.DropColumn(
            name: "IngredientCategoryId",
            table: "Ingredients");

        migrationBuilder.DropTable(
            name: "IngredientCategories");
    }
}
