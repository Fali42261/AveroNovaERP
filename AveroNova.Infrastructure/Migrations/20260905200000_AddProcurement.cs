using System;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AveroNova.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260905200000_AddProcurement")]
public partial class AddProcurement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Suppliers",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                CompanyId = table.Column<Guid>(nullable: false),
                Name = table.Column<string>(maxLength: 200, nullable: false),
                Email = table.Column<string>(nullable: false),
                Phone = table.Column<string>(nullable: false),
                Address = table.Column<string>(nullable: false),
                TaxNumber = table.Column<string>(nullable: false),
                Notes = table.Column<string>(nullable: false),
                IsActive = table.Column<bool>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false),
                UpdatedAt = table.Column<DateTime>(nullable: true),
                IsDeleted = table.Column<bool>(nullable: false),
                SyncVersion = table.Column<long>(nullable: false),
                SyncStatus = table.Column<int>(nullable: false),
                LastSyncedAt = table.Column<DateTime>(nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Suppliers", x => x.Id));

        migrationBuilder.CreateIndex(name: "IX_Suppliers_CompanyId_Name", table: "Suppliers", columns: new[] { "CompanyId", "Name" });

        migrationBuilder.CreateTable(
            name: "Purchases",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                CompanyId = table.Column<Guid>(nullable: false),
                PurchaseNumber = table.Column<string>(maxLength: 100, nullable: false),
                SupplierId = table.Column<Guid>(nullable: false),
                SupplierName = table.Column<string>(nullable: false),
                PurchaseDate = table.Column<DateTime>(nullable: false),
                DueDate = table.Column<DateTime>(nullable: false),
                ItemsJson = table.Column<string>(nullable: false),
                PaymentMethod = table.Column<int>(nullable: false),
                Reference = table.Column<string>(nullable: false),
                Notes = table.Column<string>(nullable: false),
                Status = table.Column<int>(nullable: false),
                PaidAmount = table.Column<decimal>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false),
                UpdatedAt = table.Column<DateTime>(nullable: true),
                IsDeleted = table.Column<bool>(nullable: false),
                SyncVersion = table.Column<long>(nullable: false),
                SyncStatus = table.Column<int>(nullable: false),
                LastSyncedAt = table.Column<DateTime>(nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Purchases", x => x.Id));

        migrationBuilder.CreateIndex(name: "IX_Purchases_CompanyId_PurchaseNumber", table: "Purchases", columns: new[] { "CompanyId", "PurchaseNumber" });
        migrationBuilder.CreateIndex(name: "IX_Purchases_CompanyId_SupplierId", table: "Purchases", columns: new[] { "CompanyId", "SupplierId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Purchases");
        migrationBuilder.DropTable(name: "Suppliers");
    }
}
