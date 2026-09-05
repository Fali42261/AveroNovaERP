using System;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AveroNova.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260905180000_AddStockMovements")]
public partial class AddStockMovements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StockMovements",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CompanyId = table.Column<Guid>(type: "TEXT", nullable: false),
                ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                ProductName = table.Column<string>(type: "TEXT", nullable: false),
                SKU = table.Column<string>(type: "TEXT", nullable: false),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                StockBefore = table.Column<int>(type: "INTEGER", nullable: false),
                StockAfter = table.Column<int>(type: "INTEGER", nullable: false),
                Reference = table.Column<string>(type: "TEXT", nullable: false),
                Notes = table.Column<string>(type: "TEXT", nullable: false),
                CreatedBy = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                SyncVersion = table.Column<long>(type: "INTEGER", nullable: false),
                SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                LastSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_StockMovements", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_StockMovements_CompanyId_ProductId_CreatedAt",
            table: "StockMovements",
            columns: new[] { "CompanyId", "ProductId", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "StockMovements");
    }
}
