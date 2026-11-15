using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Restaurant.Infrastructure.Persistence;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261112170000_AddCashMovementVoid")]
    public partial class AddCashMovementVoid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAtUtc",
                table: "CashMovements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoidedByUserId",
                table: "CashMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "CashMovements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_VoidedByUserId",
                table: "CashMovements",
                column: "VoidedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CashMovements_Users_VoidedByUserId",
                table: "CashMovements",
                column: "VoidedByUserId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashMovements_Users_VoidedByUserId",
                table: "CashMovements");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_VoidedByUserId",
                table: "CashMovements");

            migrationBuilder.DropColumn(name: "VoidedAtUtc", table: "CashMovements");
            migrationBuilder.DropColumn(name: "VoidedByUserId", table: "CashMovements");
            migrationBuilder.DropColumn(name: "VoidReason", table: "CashMovements");
        }
    }
}
