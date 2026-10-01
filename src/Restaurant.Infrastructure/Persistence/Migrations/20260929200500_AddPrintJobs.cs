using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Restaurant.Infrastructure.Persistence;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260929200500_AddPrintJobs")]
    public partial class AddPrintJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrintJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PayloadFormat = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrintJobs_TenantId_Status_CreatedAtUtc",
                table: "PrintJobs",
                columns: new[] { "TenantId", "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PrintJobs");
        }
    }
}
