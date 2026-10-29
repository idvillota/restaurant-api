using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Restaurant.Infrastructure.Persistence;

#nullable disable

namespace Restaurant.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261025020000_CustomerBirthdayMonthDay")]
    public partial class CustomerBirthdayMonthDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BirthMonth",
                table: "Customers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BirthDay",
                table: "Customers",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Customers"
                SET "BirthMonth" = EXTRACT(MONTH FROM "BirthDate")::integer,
                    "BirthDay" = EXTRACT(DAY FROM "BirthDate")::integer
                WHERE "BirthDate" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "Customers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "BirthDate",
                table: "Customers",
                type: "date",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Customers"
                SET "BirthDate" = MAKE_DATE(2000, "BirthMonth", "BirthDay")
                WHERE "BirthMonth" IS NOT NULL AND "BirthDay" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "BirthMonth",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "BirthDay",
                table: "Customers");
        }
    }
}
