using System;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AveroNova.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260905203000_AddReturns")]
public partial class AddReturns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SalesReturns",
            columns: table => new
            {
                Id=table.Column<Guid>(nullable:false), CompanyId=table.Column<Guid>(nullable:false), ReturnNumber=table.Column<string>(maxLength:100,nullable:false),
                InvoiceId=table.Column<Guid>(nullable:false), InvoiceNumber=table.Column<string>(nullable:false), CustomerId=table.Column<Guid>(nullable:false), CustomerName=table.Column<string>(nullable:false),
                ReturnDate=table.Column<DateTime>(nullable:false), ItemsJson=table.Column<string>(nullable:false), Reason=table.Column<string>(nullable:false), Notes=table.Column<string>(nullable:false), RefundAmount=table.Column<decimal>(nullable:false), Status=table.Column<int>(nullable:false),
                CreatedAt=table.Column<DateTime>(nullable:false), UpdatedAt=table.Column<DateTime>(nullable:true), IsDeleted=table.Column<bool>(nullable:false), SyncVersion=table.Column<long>(nullable:false), SyncStatus=table.Column<int>(nullable:false), LastSyncedAt=table.Column<DateTime>(nullable:true)
            }, constraints: table=>table.PrimaryKey("PK_SalesReturns",x=>x.Id));
        migrationBuilder.CreateIndex(name:"IX_SalesReturns_CompanyId_ReturnNumber",table:"SalesReturns",columns:new[]{"CompanyId","ReturnNumber"});
        migrationBuilder.CreateIndex(name:"IX_SalesReturns_CompanyId_InvoiceId",table:"SalesReturns",columns:new[]{"CompanyId","InvoiceId"});

        migrationBuilder.CreateTable(
            name: "PurchaseReturns",
            columns: table => new
            {
                Id=table.Column<Guid>(nullable:false), CompanyId=table.Column<Guid>(nullable:false), ReturnNumber=table.Column<string>(maxLength:100,nullable:false),
                PurchaseId=table.Column<Guid>(nullable:false), PurchaseNumber=table.Column<string>(nullable:false), SupplierId=table.Column<Guid>(nullable:false), SupplierName=table.Column<string>(nullable:false),
                ReturnDate=table.Column<DateTime>(nullable:false), ItemsJson=table.Column<string>(nullable:false), Reason=table.Column<string>(nullable:false), Notes=table.Column<string>(nullable:false), RefundAmount=table.Column<decimal>(nullable:false), Status=table.Column<int>(nullable:false),
                CreatedAt=table.Column<DateTime>(nullable:false), UpdatedAt=table.Column<DateTime>(nullable:true), IsDeleted=table.Column<bool>(nullable:false), SyncVersion=table.Column<long>(nullable:false), SyncStatus=table.Column<int>(nullable:false), LastSyncedAt=table.Column<DateTime>(nullable:true)
            }, constraints: table=>table.PrimaryKey("PK_PurchaseReturns",x=>x.Id));
        migrationBuilder.CreateIndex(name:"IX_PurchaseReturns_CompanyId_ReturnNumber",table:"PurchaseReturns",columns:new[]{"CompanyId","ReturnNumber"});
        migrationBuilder.CreateIndex(name:"IX_PurchaseReturns_CompanyId_PurchaseId",table:"PurchaseReturns",columns:new[]{"CompanyId","PurchaseId"});
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name:"PurchaseReturns");
        migrationBuilder.DropTable(name:"SalesReturns");
    }
}
