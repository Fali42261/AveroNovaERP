using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AveroNova.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260905183000_AddInvoices")]
public partial class AddInvoices : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Invoices",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CompanyId = table.Column<Guid>(type: "TEXT", nullable: false),
                InvoiceNumber = table.Column<string>(type: "TEXT", nullable: false),
                CustomerId = table.Column<Guid>(type: "TEXT", nullable: false),
                CustomerName = table.Column<string>(type: "TEXT", nullable: false),
                InvoiceDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                DueDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                ItemsJson = table.Column<string>(type: "TEXT", nullable: false),
                DiscountPct = table.Column<decimal>(type: "TEXT", nullable: false),
                TaxPct = table.Column<decimal>(type: "TEXT", nullable: false),
                PaymentMethod = table.Column<int>(type: "INTEGER", nullable: false),
                Notes = table.Column<string>(type: "TEXT", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                PaidAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                SyncVersion = table.Column<long>(type: "INTEGER", nullable: false),
                SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                LastSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Invoices", x => x.Id));

        migrationBuilder.CreateIndex(name: "IX_Invoices_CompanyId_InvoiceNumber", table: "Invoices", columns: new[] { "CompanyId", "InvoiceNumber" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Invoices_CompanyId_CustomerId", table: "Invoices", columns: new[] { "CompanyId", "CustomerId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Invoices");
    }
}
