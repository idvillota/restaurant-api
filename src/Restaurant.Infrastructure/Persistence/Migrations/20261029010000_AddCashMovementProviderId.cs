using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Restaurant.Infrastructure.Persistence;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261029010000_AddCashMovementProviderId")]
    public partial class AddCashMovementProviderId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProviderId",
                table: "CashMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_ProviderId",
                table: "CashMovements",
                column: "ProviderId");

            migrationBuilder.AddForeignKey(
                name: "FK_CashMovements_Providers_ProviderId",
                table: "CashMovements",
                column: "ProviderId",
                principalTable: "Providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashMovements_Providers_ProviderId",
                table: "CashMovements");

            migrationBuilder.DropIndex(
                name: "IX_CashMovements_ProviderId",
                table: "CashMovements");

            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "CashMovements");
        }
    }
}
