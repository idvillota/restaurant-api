using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesReceiptBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddressLine",
                table: "TenantSettings",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "TenantSettings",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "TenantSettings",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DianNextConsecutive",
                table: "TenantSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DianResolutionFrom",
                table: "TenantSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DianResolutionNumber",
                table: "TenantSettings",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DianResolutionTo",
                table: "TenantSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ImpoconsumoPercent",
                table: "TenantSettings",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumberPrefix",
                table: "TenantSettings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalName",
                table: "TenantSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LegalRepresentative",
                table: "TenantSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "TenantSettings",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "TenantSettings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxId",
                table: "TenantSettings",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TaxRegime",
                table: "TenantSettings",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TradeName",
                table: "TenantSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DianConsecutiveNumber",
                table: "Bills",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OrderNumbersSnapshot",
                table: "Bills",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessedByDisplayName",
                table: "Bills",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptPdfRelativePath",
                table: "Bills",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptXmlRelativePath",
                table: "Bills",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TableCodesSnapshot",
                table: "Bills",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BillLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillId = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesOrderLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductTypeName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ImpoconsumoAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillLines_Bills_BillId",
                        column: x => x.BillId,
                        principalTable: "Bills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_TenantId_DianConsecutiveNumber",
                table: "Bills",
                columns: new[] { "TenantId", "DianConsecutiveNumber" },
                unique: true,
                filter: "\"DianConsecutiveNumber\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_BillLines_BillId",
                table: "BillLines",
                column: "BillId");

            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT "Id",
                           ROW_NUMBER() OVER (PARTITION BY "TenantId" ORDER BY "IssuedAtUtc") AS rn
                    FROM "Bills"
                )
                UPDATE "Bills" b
                SET "DianConsecutiveNumber" = ranked.rn
                FROM ranked
                WHERE b."Id" = ranked."Id";

                UPDATE "TenantSettings"
                SET "ImpoconsumoPercent" = 8
                WHERE "ImpoconsumoPercent" = 0;

                UPDATE "TenantSettings"
                SET "Country" = 'Colombia'
                WHERE "Country" = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillLines");

            migrationBuilder.DropIndex(
                name: "IX_Bills_TenantId_DianConsecutiveNumber",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "AddressLine",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "City",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "DianNextConsecutive",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "DianResolutionFrom",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "DianResolutionNumber",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "DianResolutionTo",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "ImpoconsumoPercent",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "InvoiceNumberPrefix",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "LegalName",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "LegalRepresentative",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "TaxId",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "TaxRegime",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "TradeName",
                table: "TenantSettings");

            migrationBuilder.DropColumn(
                name: "DianConsecutiveNumber",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "OrderNumbersSnapshot",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "ProcessedByDisplayName",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "ReceiptPdfRelativePath",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "ReceiptXmlRelativePath",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "TableCodesSnapshot",
                table: "Bills");
        }
    }
}
