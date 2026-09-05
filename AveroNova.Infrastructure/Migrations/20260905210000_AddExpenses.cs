using System;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AveroNova.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260905210000_AddExpenses")]
public partial class AddExpenses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Expenses",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                CompanyId = table.Column<Guid>(nullable: false),
                Category = table.Column<string>(maxLength: 100, nullable: false),
                Description = table.Column<string>(nullable: false),
                Amount = table.Column<decimal>(nullable: false),
                ExpenseDate = table.Column<DateTime>(nullable: false),
                Method = table.Column<int>(nullable: false),
                Reference = table.Column<string>(maxLength: 100, nullable: false),
                Notes = table.Column<string>(nullable: false),
                Status = table.Column<int>(nullable: false),
                ApprovedBy = table.Column<string>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false),
                UpdatedAt = table.Column<DateTime>(nullable: true),
                IsDeleted = table.Column<bool>(nullable: false),
                SyncVersion = table.Column<long>(nullable: false),
                SyncStatus = table.Column<int>(nullable: false),
                LastSyncedAt = table.Column<DateTime>(nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Expenses", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_Expenses_CompanyId_ExpenseDate",
            table: "Expenses",
            columns: new[] { "CompanyId", "ExpenseDate" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Expenses");
    }
}
