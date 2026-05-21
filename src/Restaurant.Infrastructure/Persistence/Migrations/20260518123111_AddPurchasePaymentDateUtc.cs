using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchasePaymentDateUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentDateUtc",
                table: "Purchases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """UPDATE "Purchases" SET "PaymentDateUtc" = "PurchasedAtUtc" WHERE "PaymentDateUtc" IS NULL;""");

            migrationBuilder.Sql(
                """ALTER TABLE "Purchases" ALTER COLUMN "PaymentDateUtc" SET NOT NULL;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentDateUtc",
                table: "Purchases");
        }
    }
}
