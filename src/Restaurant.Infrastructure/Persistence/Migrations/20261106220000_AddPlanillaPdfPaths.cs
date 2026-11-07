using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Restaurant.Infrastructure.Persistence;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261106220000_AddPlanillaPdfPaths")]
    public partial class AddPlanillaPdfPaths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClosurePdfRelativePath",
                table: "CashierShifts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanillaPdfRelativePath",
                table: "DailyClosures",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlanillaTotalSales",
                table: "DailyClosures",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PlanillaTotalEgresos",
                table: "DailyClosures",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PlanillaOverShort",
                table: "DailyClosures",
                type: "numeric(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosurePdfRelativePath",
                table: "CashierShifts");

            migrationBuilder.DropColumn(
                name: "PlanillaPdfRelativePath",
                table: "DailyClosures");

            migrationBuilder.DropColumn(
                name: "PlanillaTotalSales",
                table: "DailyClosures");

            migrationBuilder.DropColumn(
                name: "PlanillaTotalEgresos",
                table: "DailyClosures");

            migrationBuilder.DropColumn(
                name: "PlanillaOverShort",
                table: "DailyClosures");
        }
    }
}
