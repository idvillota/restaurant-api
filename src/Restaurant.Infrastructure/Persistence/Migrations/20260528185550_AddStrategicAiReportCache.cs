using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStrategicAiReportCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StrategicAiReportCaches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SalesEndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CacheDate = table.Column<DateOnly>(type: "date", nullable: false),
                    HtmlContent = table.Column<string>(type: "text", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategicAiReportCaches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StrategicAiReportCaches_TenantId_SalesStartDate_SalesEndDat~",
                table: "StrategicAiReportCaches",
                columns: new[] { "TenantId", "SalesStartDate", "SalesEndDate", "CacheDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StrategicAiReportCaches");
        }
    }
}
