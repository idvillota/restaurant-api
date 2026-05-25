using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncPendingBillingModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bills_TenantId_DianConsecutiveNumber",
                table: "Bills");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_TenantId_DianConsecutiveNumber",
                table: "Bills",
                columns: new[] { "TenantId", "DianConsecutiveNumber" },
                unique: true,
                filter: "\"DianConsecutiveNumber\" > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bills_TenantId_DianConsecutiveNumber",
                table: "Bills");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_TenantId_DianConsecutiveNumber",
                table: "Bills",
                columns: new[] { "TenantId", "DianConsecutiveNumber" },
                unique: true);
        }
    }
}
