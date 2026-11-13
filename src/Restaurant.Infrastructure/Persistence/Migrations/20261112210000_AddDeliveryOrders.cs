using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Restaurant.Infrastructure.Persistence;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261112210000_AddDeliveryOrders")]
    public partial class AddDeliveryOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Customers",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeliverySlot",
                table: "DiningTables",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDelivery",
                table: "SalesOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryCustomerName",
                table: "SalesOrders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryPhone",
                table: "SalesOrders",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryAddress",
                table: "SalesOrders",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Address", table: "Customers");
            migrationBuilder.DropColumn(name: "IsDeliverySlot", table: "DiningTables");
            migrationBuilder.DropColumn(name: "IsDelivery", table: "SalesOrders");
            migrationBuilder.DropColumn(name: "DeliveryCustomerName", table: "SalesOrders");
            migrationBuilder.DropColumn(name: "DeliveryPhone", table: "SalesOrders");
            migrationBuilder.DropColumn(name: "DeliveryAddress", table: "SalesOrders");
        }
    }
}
