using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTableServiceOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DiningTableId",
                table: "SalesOrders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenedAtUtc",
                table: "SalesOrders",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "SalesOrderLines",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SalesOrderLineExcludedIngredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesOrderLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesOrderLineExcludedIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesOrderLineExcludedIngredients_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SalesOrderLineExcludedIngredients_SalesOrderLines_SalesOrde~",
                        column: x => x.SalesOrderLineId,
                        principalTable: "SalesOrderLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_DiningTableId",
                table: "SalesOrders",
                column: "DiningTableId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_TenantId_DiningTableId_Status",
                table: "SalesOrders",
                columns: new[] { "TenantId", "DiningTableId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderLineExcludedIngredients_IngredientId",
                table: "SalesOrderLineExcludedIngredients",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderLineExcludedIngredients_SalesOrderLineId_Ingredie~",
                table: "SalesOrderLineExcludedIngredients",
                columns: new[] { "SalesOrderLineId", "IngredientId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrders_DiningTables_DiningTableId",
                table: "SalesOrders",
                column: "DiningTableId",
                principalTable: "DiningTables",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrders_DiningTables_DiningTableId",
                table: "SalesOrders");

            migrationBuilder.DropTable(
                name: "SalesOrderLineExcludedIngredients");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_DiningTableId",
                table: "SalesOrders");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_TenantId_DiningTableId_Status",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "DiningTableId",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "OpenedAtUtc",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "SalesOrderLines");
        }
    }
}
