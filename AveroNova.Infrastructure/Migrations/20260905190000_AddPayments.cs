using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AveroNova.Infrastructure.Migrations;

public partial class AddPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Payments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                CompanyId = table.Column<Guid>(type: "TEXT", nullable: false),
                PaymentNumber = table.Column<string>(type: "TEXT", nullable: false),
                PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                PartyName = table.Column<string>(type: "TEXT", nullable: false),
                IsSupplier = table.Column<bool>(type: "INTEGER", nullable: false),
                InvoiceId = table.Column<Guid>(type: "TEXT", nullable: true),
                InvoiceNumber = table.Column<string>(type: "TEXT", nullable: false),
                Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                Method = table.Column<int>(type: "INTEGER", nullable: false),
                PaymentDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                Reference = table.Column<string>(type: "TEXT", nullable: false),
                Notes = table.Column<string>(type: "TEXT", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                SyncVersion = table.Column<long>(type: "INTEGER", nullable: false),
                SyncStatus = table.Column<int>(type: "INTEGER", nullable: false),
                LastSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_Payments", x => x.Id));

        migrationBuilder.CreateIndex("IX_Payments_CompanyId_PaymentNumber", "Payments", new[] { "CompanyId", "PaymentNumber" }, unique: true);
        migrationBuilder.CreateIndex("IX_Payments_InvoiceId", "Payments", "InvoiceId");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("Payments");
}
