using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AveroNova.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260905170000_AddCustomers")]
public partial class AddCustomers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Customers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CompanyId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                Email = table.Column<string>(type: "TEXT", nullable: false),
                Phone = table.Column<string>(type: "TEXT", nullable: false),
                Address = table.Column<string>(type: "TEXT", nullable: false),
                City = table.Column<string>(type: "TEXT", nullable: false),
                Country = table.Column<string>(type: "TEXT", nullable: false),
                TaxNumber = table.Column<string>(type: "TEXT", nullable: false),
                Notes = table.Column<string>(type: "TEXT", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                OutstandingBalance = table.Column<decimal>(type: "TEXT", nullable: false),
                TotalPurchases = table.Column<decimal>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                SyncVersion = table.Column<long>(type: "INTEGER", nullable: false),
                SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                LastSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Customers", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Customers_CompanyId_Name",
            table: "Customers",
            columns: new[] { "CompanyId", "Name" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Customers");
    }
}
