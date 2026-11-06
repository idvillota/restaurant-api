using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Restaurant.Infrastructure.Persistence;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261105230000_DropVitrinaCafeProductType")]
    public partial class DropVitrinaCafeProductType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsVitrinaCafe",
                table: "ProductTypes");

            migrationBuilder.DropColumn(
                name: "IsVitrinaCafe",
                table: "BillLines");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVitrinaCafe",
                table: "ProductTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsVitrinaCafe",
                table: "BillLines",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
