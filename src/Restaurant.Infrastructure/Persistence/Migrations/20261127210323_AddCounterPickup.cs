using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCounterPickup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CounterServiceEnabled",
                table: "TenantSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CounterAbandonedAtUtc",
                table: "SalesOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CounterCustomerName",
                table: "SalesOrders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CounterHandedOverAtUtc",
                table: "SalesOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CounterNotificationError",
                table: "SalesOrders",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CounterNotifiedAtUtc",
                table: "SalesOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CounterPhone",
                table: "SalesOrders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CounterReadyAtUtc",
                table: "SalesOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCounterPickup",
                table: "SalesOrders",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CounterServiceEnabled",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "CounterAbandonedAtUtc",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "CounterCustomerName",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "CounterHandedOverAtUtc",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "CounterNotificationError",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "CounterNotifiedAtUtc",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "CounterPhone",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "CounterReadyAtUtc",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "IsCounterPickup",
                table: "SalesOrders");
        }
    }
}
