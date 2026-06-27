using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantUserAppearancePreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BrandTheme",
                table: "TenantUsers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "operations");

            migrationBuilder.AddColumn<string>(
                name: "ColorScheme",
                table: "TenantUsers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "auto");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BrandTheme",
                table: "TenantUsers");

            migrationBuilder.DropColumn(
                name: "ColorScheme",
                table: "TenantUsers");
        }
    }
}
