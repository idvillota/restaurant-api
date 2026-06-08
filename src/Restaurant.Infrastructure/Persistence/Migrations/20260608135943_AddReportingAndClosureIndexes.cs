using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingAndClosureIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_TenantId_ClosedAtUtc",
                table: "SalesOrders",
                columns: new[] { "TenantId", "ClosedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_TenantId_PaidAtUtc",
                table: "Bills",
                columns: new[] { "TenantId", "PaidAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_TenantId_ClosedAtUtc",
                table: "SalesOrders");

            migrationBuilder.DropIndex(
                name: "IX_Bills_TenantId_PaidAtUtc",
                table: "Bills");
        }
    }
}
