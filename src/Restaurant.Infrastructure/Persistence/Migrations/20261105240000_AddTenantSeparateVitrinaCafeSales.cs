using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Restaurant.Infrastructure.Persistence;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261105240000_AddTenantSeparateVitrinaCafeSales")]
    public partial class AddTenantSeparateVitrinaCafeSales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SeparateVitrinaCafeSales",
                table: "TenantSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE "TenantSettings" AS s
                SET "SeparateVitrinaCafeSales" = TRUE
                FROM "Tenants" AS t
                WHERE s."TenantId" = t."Id"
                  AND (
                    t."Name" ILIKE '%nuestro%caf%'
                    OR t."Slug" ILIKE '%nuestro%cafe%'
                    OR s."TradeName" ILIKE '%nuestro%caf%'
                    OR s."LegalName" ILIKE '%nuestro%caf%'
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SeparateVitrinaCafeSales",
                table: "TenantSettings");
        }
    }
}
