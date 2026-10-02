using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Restaurant.Infrastructure.Persistence;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260928184500_AddMobileSyncReceipts")]
    public partial class AddMobileSyncReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MobileSyncReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LocalOrderId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RemoteOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineIds = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MobileSyncReceipts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MobileSyncReceipts_TenantId_DeviceId_LocalOrderId",
                table: "MobileSyncReceipts",
                columns: new[] { "TenantId", "DeviceId", "LocalOrderId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "MobileSyncReceipts");
        }
    }
}
