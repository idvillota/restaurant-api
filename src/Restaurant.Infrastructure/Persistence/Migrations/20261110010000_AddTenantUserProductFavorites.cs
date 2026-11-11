using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Restaurant.Infrastructure.Persistence;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261110010000_AddTenantUserProductFavorites")]
    public partial class AddTenantUserProductFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantUserProductFavorites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantUserProductFavorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantUserProductFavorites_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenantUserProductFavorites_TenantUsers_TenantUserId",
                        column: x => x.TenantUserId,
                        principalTable: "TenantUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantUserProductFavorites_ProductId",
                table: "TenantUserProductFavorites",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantUserProductFavorites_TenantUserId_ProductId",
                table: "TenantUserProductFavorites",
                columns: new[] { "TenantUserId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantUserProductFavorites_TenantId_TenantUserId_SortOrder",
                table: "TenantUserProductFavorites",
                columns: new[] { "TenantId", "TenantUserId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TenantUserProductFavorites");
        }
    }
}
