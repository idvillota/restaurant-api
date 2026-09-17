using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Restaurant.Infrastructure.Persistence;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260916023000_AddSalesOrderLogicalCancel")]
    public partial class AddSalesOrderLogicalCancel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "SalesOrders",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoidedByUserId",
                table: "SalesOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "SalesOrderLines",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAtUtc",
                table: "SalesOrderLines",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelledByUserId",
                table: "SalesOrderLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CancelledQuantity",
                table: "SalesOrderLines",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "VoidedByUserId",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "CancelReason",
                table: "SalesOrderLines");

            migrationBuilder.DropColumn(
                name: "CancelledAtUtc",
                table: "SalesOrderLines");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                table: "SalesOrderLines");

            migrationBuilder.DropColumn(
                name: "CancelledQuantity",
                table: "SalesOrderLines");
        }
    }
}
