using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQueryPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesOrderLines_SalesOrderId",
                table: "SalesOrderLines");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CashierShiftId",
                table: "Payments");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_TenantId_Status",
                table: "SalesOrders",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderLines_SalesOrderId_SentToKitchenAtUtc",
                table: "SalesOrderLines",
                columns: new[] { "SalesOrderId", "SentToKitchenAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderLines_TenantId_CreatedAtUtc",
                table: "SalesOrderLines",
                columns: new[] { "TenantId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_TenantId_PaymentDateUtc",
                table: "Purchases",
                columns: new[] { "TenantId", "PaymentDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_TenantId_PurchasedAtUtc",
                table: "Purchases",
                columns: new[] { "TenantId", "PurchasedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CashierShiftId_Status",
                table: "Payments",
                columns: new[] { "CashierShiftId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_TenantId_TaxId",
                table: "Customers",
                columns: new[] { "TenantId", "TaxId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_TenantId_Status",
                table: "SalesOrders");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrderLines_SalesOrderId_SentToKitchenAtUtc",
                table: "SalesOrderLines");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrderLines_TenantId_CreatedAtUtc",
                table: "SalesOrderLines");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_TenantId_PaymentDateUtc",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_TenantId_PurchasedAtUtc",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CashierShiftId_Status",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Customers_TenantId_TaxId",
                table: "Customers");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderLines_SalesOrderId",
                table: "SalesOrderLines",
                column: "SalesOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CashierShiftId",
                table: "Payments",
                column: "CashierShiftId");
        }
    }
}
