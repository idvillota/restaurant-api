using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductUnitStockControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StockControlMode",
                table: "TenantSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill from legacy InventoryEnabled: true → Ingredients (0), false → None (1).
            migrationBuilder.Sql(
                """
                UPDATE "TenantSettings"
                SET "StockControlMode" = CASE WHEN "InventoryEnabled" THEN 0 ELSE 1 END;
                """);

            migrationBuilder.AddColumn<decimal>(
                name: "StockQuantity",
                table: "Products",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "ProductProductionDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductProductionDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductProductionDocuments_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductProductionLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductProductionDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    StockQuantitySnapshot = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductProductionLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductProductionLines_ProductProductionDocuments_ProductPr~",
                        column: x => x.ProductProductionDocumentId,
                        principalTable: "ProductProductionDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductProductionLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductProductionDocuments_CreatedByUserId",
                table: "ProductProductionDocuments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductProductionDocuments_TenantId_DocumentNumber",
                table: "ProductProductionDocuments",
                columns: new[] { "TenantId", "DocumentNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductProductionDocuments_TenantId_OccurredAtUtc",
                table: "ProductProductionDocuments",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductProductionLines_ProductId",
                table: "ProductProductionLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductProductionLines_ProductProductionDocumentId_ProductId",
                table: "ProductProductionLines",
                columns: new[] { "ProductProductionDocumentId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductProductionLines_TenantId_ProductId",
                table: "ProductProductionLines",
                columns: new[] { "TenantId", "ProductId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductProductionLines");

            migrationBuilder.DropTable(
                name: "ProductProductionDocuments");

            migrationBuilder.DropColumn(
                name: "StockControlMode",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "StockQuantity",
                table: "Products");
        }
    }
}
