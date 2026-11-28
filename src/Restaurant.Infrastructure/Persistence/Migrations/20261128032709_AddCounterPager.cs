using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCounterPager : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CounterUsesPager",
                table: "TenantSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CounterPagerNumber",
                table: "SalesOrders",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CounterUsesPager",
                table: "SalesOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CounterUsesPager",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "CounterPagerNumber",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "CounterUsesPager",
                table: "SalesOrders");
        }
    }
}
