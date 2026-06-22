using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIngredientMovementDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IngredientMovementDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientMovementTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientMovementDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngredientMovementDocuments_IngredientMovementTypes_Ingredi~",
                        column: x => x.IngredientMovementTypeId,
                        principalTable: "IngredientMovementTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IngredientMovementDocuments_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "IngredientMovementDocumentId",
                table: "IngredientMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                INSERT INTO "IngredientMovementDocuments" (
                    "Id", "TenantId", "IngredientMovementTypeId", "DocumentNumber", "Notes",
                    "CreatedByUserId", "OccurredAtUtc", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT
                    m."Id",
                    m."TenantId",
                    m."IngredientMovementTypeId",
                    'DOC-' || LEFT(REPLACE(m."Id"::text, '-', ''), 12),
                    m."Notes",
                    m."CreatedByUserId",
                    m."OccurredAtUtc",
                    m."CreatedAtUtc",
                    m."UpdatedAtUtc"
                FROM "IngredientMovements" m;

                UPDATE "IngredientMovements"
                SET "IngredientMovementDocumentId" = "Id";
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_IngredientMovements_IngredientMovementTypes_IngredientMovem~",
                table: "IngredientMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_IngredientMovements_Users_CreatedByUserId",
                table: "IngredientMovements");

            migrationBuilder.DropIndex(
                name: "IX_IngredientMovements_CreatedByUserId",
                table: "IngredientMovements");

            migrationBuilder.DropIndex(
                name: "IX_IngredientMovements_IngredientMovementTypeId",
                table: "IngredientMovements");

            migrationBuilder.DropIndex(
                name: "IX_IngredientMovements_TenantId_IngredientId_OccurredAtUtc",
                table: "IngredientMovements");

            migrationBuilder.DropIndex(
                name: "IX_IngredientMovements_TenantId_OccurredAtUtc",
                table: "IngredientMovements");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "IngredientMovements");

            migrationBuilder.DropColumn(
                name: "IngredientMovementTypeId",
                table: "IngredientMovements");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "IngredientMovements");

            migrationBuilder.DropColumn(
                name: "OccurredAtUtc",
                table: "IngredientMovements");

            migrationBuilder.AlterColumn<Guid>(
                name: "IngredientMovementDocumentId",
                table: "IngredientMovements",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientMovements_IngredientMovementDocumentId_Ingredient~",
                table: "IngredientMovements",
                columns: new[] { "IngredientMovementDocumentId", "IngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientMovements_TenantId_IngredientId",
                table: "IngredientMovements",
                columns: new[] { "TenantId", "IngredientId" });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientMovementDocuments_CreatedByUserId",
                table: "IngredientMovementDocuments",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientMovementDocuments_IngredientMovementTypeId",
                table: "IngredientMovementDocuments",
                column: "IngredientMovementTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientMovementDocuments_TenantId_DocumentNumber",
                table: "IngredientMovementDocuments",
                columns: new[] { "TenantId", "DocumentNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientMovementDocuments_TenantId_OccurredAtUtc",
                table: "IngredientMovementDocuments",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_IngredientMovements_IngredientMovementDocuments_IngredientM~",
                table: "IngredientMovements",
                column: "IngredientMovementDocumentId",
                principalTable: "IngredientMovementDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IngredientMovements_IngredientMovementDocuments_IngredientM~",
                table: "IngredientMovements");

            migrationBuilder.DropIndex(
                name: "IX_IngredientMovements_IngredientMovementDocumentId_Ingredient~",
                table: "IngredientMovements");

            migrationBuilder.DropIndex(
                name: "IX_IngredientMovements_TenantId_IngredientId",
                table: "IngredientMovements");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "IngredientMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IngredientMovementTypeId",
                table: "IngredientMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "IngredientMovements",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurredAtUtc",
                table: "IngredientMovements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "IngredientMovements" m
                SET
                    "CreatedByUserId" = d."CreatedByUserId",
                    "IngredientMovementTypeId" = d."IngredientMovementTypeId",
                    "Notes" = d."Notes",
                    "OccurredAtUtc" = d."OccurredAtUtc"
                FROM "IngredientMovementDocuments" d
                WHERE m."IngredientMovementDocumentId" = d."Id";
                """);

            migrationBuilder.DropColumn(
                name: "IngredientMovementDocumentId",
                table: "IngredientMovements");

            migrationBuilder.DropTable(
                name: "IngredientMovementDocuments");

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByUserId",
                table: "IngredientMovements",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "IngredientMovementTypeId",
                table: "IngredientMovements",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "OccurredAtUtc",
                table: "IngredientMovements",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IngredientMovements_CreatedByUserId",
                table: "IngredientMovements",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientMovements_IngredientMovementTypeId",
                table: "IngredientMovements",
                column: "IngredientMovementTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_IngredientMovements_TenantId_IngredientId_OccurredAtUtc",
                table: "IngredientMovements",
                columns: new[] { "TenantId", "IngredientId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientMovements_TenantId_OccurredAtUtc",
                table: "IngredientMovements",
                columns: new[] { "TenantId", "OccurredAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_IngredientMovements_IngredientMovementTypes_IngredientMovem~",
                table: "IngredientMovements",
                column: "IngredientMovementTypeId",
                principalTable: "IngredientMovementTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IngredientMovements_Users_CreatedByUserId",
                table: "IngredientMovements",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
