using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Restaurant.Infrastructure.Persistence;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261118010000_AddLunchTicketsAndTiqueteras")]
    public partial class AddLunchTicketsAndTiqueteras : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TiqueterasEnabled",
                table: "TenantSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LunchTicketPackSize",
                table: "Products",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLunchTicketRedeemable",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LunchTicketBooklets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseBillId = table.Column<Guid>(type: "uuid", nullable: true),
                    PackProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalTickets = table.Column<int>(type: "integer", nullable: false),
                    RemainingTickets = table.Column<int>(type: "integer", nullable: false),
                    PackUnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PurchasedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LunchTicketBooklets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LunchTicketBooklets_Bills_PurchaseBillId",
                        column: x => x.PurchaseBillId,
                        principalTable: "Bills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LunchTicketBooklets_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LunchTicketBooklets_Products_PackProductId",
                        column: x => x.PackProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LunchTicketBooklets_CustomerId",
                table: "LunchTicketBooklets",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_LunchTicketBooklets_PackProductId",
                table: "LunchTicketBooklets",
                column: "PackProductId");

            migrationBuilder.CreateIndex(
                name: "IX_LunchTicketBooklets_PurchaseBillId",
                table: "LunchTicketBooklets",
                column: "PurchaseBillId");

            migrationBuilder.CreateIndex(
                name: "IX_LunchTicketBooklets_TenantId_CustomerId_RemainingTickets",
                table: "LunchTicketBooklets",
                columns: new[] { "TenantId", "CustomerId", "RemainingTickets" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "LunchTicketBooklets");
            migrationBuilder.DropColumn(name: "TiqueterasEnabled", table: "TenantSettings");
            migrationBuilder.DropColumn(name: "LunchTicketPackSize", table: "Products");
            migrationBuilder.DropColumn(name: "IsLunchTicketRedeemable", table: "Products");
        }
    }
}
