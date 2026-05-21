using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiningTableFloorLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "LayoutX",
                table: "DiningTables",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LayoutY",
                table: "DiningTables",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LayoutX",
                table: "DiningTables");

            migrationBuilder.DropColumn(
                name: "LayoutY",
                table: "DiningTables");
        }
    }
}
