using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStrategicReportCacheType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StrategicAiReportCaches_TenantId_SalesStartDate_SalesEndDat~",
                table: "StrategicAiReportCaches");

            migrationBuilder.AddColumn<int>(
                name: "ForecastDays",
                table: "StrategicAiReportCaches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportType",
                table: "StrategicAiReportCaches",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "legacy_ai");

            migrationBuilder.CreateIndex(
                name: "IX_StrategicAiReportCaches_TenantId_ReportType_SalesStartDate_~",
                table: "StrategicAiReportCaches",
                columns: new[] { "TenantId", "ReportType", "SalesStartDate", "SalesEndDate", "ForecastDays", "CacheDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StrategicAiReportCaches_TenantId_ReportType_SalesStartDate_~",
                table: "StrategicAiReportCaches");

            migrationBuilder.DropColumn(
                name: "ForecastDays",
                table: "StrategicAiReportCaches");

            migrationBuilder.DropColumn(
                name: "ReportType",
                table: "StrategicAiReportCaches");

            migrationBuilder.CreateIndex(
                name: "IX_StrategicAiReportCaches_TenantId_SalesStartDate_SalesEndDat~",
                table: "StrategicAiReportCaches",
                columns: new[] { "TenantId", "SalesStartDate", "SalesEndDate", "CacheDate" },
                unique: true);
        }
    }
}
