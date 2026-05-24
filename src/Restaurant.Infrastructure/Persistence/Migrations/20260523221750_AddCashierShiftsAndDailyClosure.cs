using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashierShiftsAndDailyClosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OperationalDayCutoffHour",
                table: "TenantSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "CashierShiftId",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProcessedByUserId",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CashierShiftId",
                table: "Bills",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProcessedByUserId",
                table: "Bills",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CashierShifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashierUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OpeningFloat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpectedCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CountedCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ClosingNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashierShifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashierShifts_Users_CashierUserId",
                        column: x => x.CashierUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DailyClosures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyClosures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyClosures_Users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CashMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashierShiftId = table.Column<Guid>(type: "uuid", nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    MovementType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashMovements_CashierShifts_CashierShiftId",
                        column: x => x.CashierShiftId,
                        principalTable: "CashierShifts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CashMovements_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "Purchases",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CashMovements_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CashierShiftId",
                table: "Payments",
                column: "CashierShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_CashierShiftId",
                table: "Bills",
                column: "CashierShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_CashierUserId",
                table: "CashierShifts",
                column: "CashierUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_TenantId_BusinessDate",
                table: "CashierShifts",
                columns: new[] { "TenantId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_TenantId_CashierUserId_Status",
                table: "CashierShifts",
                columns: new[] { "TenantId", "CashierUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_CashierShiftId",
                table: "CashMovements",
                column: "CashierShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_CreatedByUserId",
                table: "CashMovements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_PurchaseId",
                table: "CashMovements",
                column: "PurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CashMovements_TenantId_BusinessDate",
                table: "CashMovements",
                columns: new[] { "TenantId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosures_ClosedByUserId",
                table: "DailyClosures",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyClosures_TenantId_BusinessDate",
                table: "DailyClosures",
                columns: new[] { "TenantId", "BusinessDate" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Bills_CashierShifts_CashierShiftId",
                table: "Bills",
                column: "CashierShiftId",
                principalTable: "CashierShifts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_CashierShifts_CashierShiftId",
                table: "Payments",
                column: "CashierShiftId",
                principalTable: "CashierShifts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bills_CashierShifts_CashierShiftId",
                table: "Bills");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_CashierShifts_CashierShiftId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "CashMovements");

            migrationBuilder.DropTable(
                name: "DailyClosures");

            migrationBuilder.DropTable(
                name: "CashierShifts");

            migrationBuilder.DropIndex(
                name: "IX_Payments_CashierShiftId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Bills_CashierShiftId",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "OperationalDayCutoffHour",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "CashierShiftId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProcessedByUserId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CashierShiftId",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "ProcessedByUserId",
                table: "Bills");
        }
    }
}
