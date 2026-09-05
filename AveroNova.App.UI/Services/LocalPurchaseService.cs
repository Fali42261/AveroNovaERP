using System.Text.Json;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services;

public sealed class LocalPurchaseService : IPurchaseService
{
    private readonly IDbContextFactory<LocalAppDbContext> _factory;
    private readonly IAppSessionContext _session;
    public LocalPurchaseService(IDbContextFactory<LocalAppDbContext> factory, IAppSessionContext session) { _factory=factory; _session=session; }

    public async Task<List<PurchaseModel>> GetAllAsync(Guid companyId) { if (!Allows(companyId)) return []; await using var db=await _factory.CreateDbContextAsync(); return (await db.Purchases.AsNoTracking().Where(x=>x.CompanyId==companyId).OrderByDescending(x=>x.PurchaseDate).ToListAsync()).Select(Map).ToList(); }
    public async Task<PurchaseModel?> GetByIdAsync(Guid id) { await using var db=await _factory.CreateDbContextAsync(); var x=await db.Purchases.AsNoTracking().FirstOrDefaultAsync(p=>p.Id==id); return x is null || !Allows(x.CompanyId) ? null : Map(x); }

    public async Task<(bool Ok,string? Error)> CreateAsync(PurchaseModel m)
    {
        var error=await ValidateAsync(m); if(error is not null) return(false,error);
        await using var db=await _factory.CreateDbContextAsync();
        if(await db.Purchases.AnyAsync(x=>x.CompanyId==m.CompanyId && x.PurchaseNumber==m.PurchaseNumber.Trim())) return(false,"Purchase number already exists.");
        var now=DateTime.UtcNow; m.LocalId=m.LocalId==Guid.Empty?Guid.NewGuid():m.LocalId; var row=ToEntity(m,now); db.Purchases.Add(row);
        LocalSyncQueueWriter.Enqueue(db,"Purchase",row.Id,row.CompanyId,SyncOperation.Create,Payload(row),now); await db.SaveChangesAsync(); return(true,null);
    }

    public async Task<(bool Ok,string? Error)> UpdateAsync(PurchaseModel m)
    {
        var error=await ValidateAsync(m); if(error is not null) return(false,error);
        await using var db=await _factory.CreateDbContextAsync(); var row=await db.Purchases.FirstOrDefaultAsync(x=>x.Id==m.LocalId);
        if(row is null || !Allows(row.CompanyId) || row.CompanyId!=m.CompanyId) return(false,"Purchase not found.");
        if(await db.Purchases.AnyAsync(x=>x.CompanyId==m.CompanyId && x.Id!=m.LocalId && x.PurchaseNumber==m.PurchaseNumber.Trim())) return(false,"Purchase number already exists.");
        Apply(row,m,DateTime.UtcNow); LocalSyncQueueWriter.Enqueue(db,"Purchase",row.Id,row.CompanyId,SyncOperation.Update,Payload(row),row.UpdatedAtUtc); await db.SaveChangesAsync(); return(true,null);
    }

    public async Task<(bool Ok,string? Error)> DeleteAsync(Guid id) { await using var db=await _factory.CreateDbContextAsync(); var row=await db.Purchases.FirstOrDefaultAsync(x=>x.Id==id); if(row is null || !Allows(row.CompanyId)) return(false,"Purchase not found."); db.Purchases.Remove(row); LocalSyncQueueWriter.Enqueue(db,"Purchase",row.Id,row.CompanyId,SyncOperation.Delete,new{row.Id,row.CompanyId},DateTime.UtcNow); await db.SaveChangesAsync(); return(true,null); }
    public async Task<string> GetNextPurchaseNumberAsync(Guid companyId) { if(!Allows(companyId)) return string.Empty; await using var db=await _factory.CreateDbContextAsync(); var prefix=$"PO-{DateTime.Today:yyyy}-"; var nums=await db.Purchases.AsNoTracking().Where(x=>x.CompanyId==companyId && x.PurchaseNumber.StartsWith(prefix)).Select(x=>x.PurchaseNumber).ToListAsync(); var max=nums.Select(x=>int.TryParse(x[prefix.Length..],out var n)?n:0).DefaultIfEmpty().Max(); return $"{prefix}{max+1:D4}"; }

    private async Task<string?> ValidateAsync(PurchaseModel m) { if(!Allows(m.CompanyId)) return "You do not have access to this company."; if(m.SupplierId==Guid.Empty) return "Select a supplier."; if(string.IsNullOrWhiteSpace(m.PurchaseNumber)) return "Purchase number is required."; if(m.DueDate.Date<m.PurchaseDate.Date) return "Due date cannot be before purchase date."; if(m.Items.Count==0) return "Add at least one purchase item."; if(m.Items.Any(x=>x.ProductId==Guid.Empty || x.Quantity<=0 || x.UnitPrice<0 || x.TaxPct<0 || x.TaxPct>100)) return "Purchase item values are invalid."; if(m.PaidAmount<0 || m.PaidAmount>m.GrandTotal) return "Paid amount must be between zero and grand total."; await using var db=await _factory.CreateDbContextAsync(); return await db.Suppliers.AnyAsync(x=>x.Id==m.SupplierId && x.CompanyId==m.CompanyId && x.IsActive)?null:"Supplier not found for this company."; }
    private bool Allows(Guid id)=>id!=Guid.Empty && _session.CurrentCompanyId==id;
    private static PurchaseModel Map(LocalPurchaseEntity x)=>new(){LocalId=x.Id,ServerId=x.ServerId?.ToString("D"),CompanyId=x.CompanyId,PurchaseNumber=x.PurchaseNumber,SupplierId=x.SupplierId,SupplierName=x.SupplierName,PurchaseDate=x.PurchaseDate,DueDate=x.DueDate,Items=JsonSerializer.Deserialize<List<PurchaseLineItem>>(x.ItemsJson)??[],PaymentMethod=(PaymentMethod)x.PaymentMethod,Reference=x.Reference,Notes=x.Notes,Status=(PurchaseStatus)x.Status,PaidAmount=x.PaidAmount,SyncStatus=ToUi(x.SyncStatus),CreatedAt=x.CreatedAtUtc,UpdatedAt=x.UpdatedAtUtc,LastSyncedAt=x.LastSyncedAtUtc};
    private static LocalPurchaseEntity ToEntity(PurchaseModel m,DateTime now)=>new(){Id=m.LocalId,CompanyId=m.CompanyId,PurchaseNumber=m.PurchaseNumber.Trim(),SupplierId=m.SupplierId,SupplierName=m.SupplierName.Trim(),PurchaseDate=m.PurchaseDate.Date,DueDate=m.DueDate.Date,ItemsJson=JsonSerializer.Serialize(m.Items),PaymentMethod=(int)m.PaymentMethod,Reference=m.Reference.Trim(),Notes=m.Notes.Trim(),Status=(int)m.Status,PaidAmount=m.PaidAmount,SyncStatus=(int)RecordSyncStatus.Pending,CreatedAtUtc=now,UpdatedAtUtc=now};
    private static void Apply(LocalPurchaseEntity x,PurchaseModel m,DateTime now){x.PurchaseNumber=m.PurchaseNumber.Trim();x.SupplierId=m.SupplierId;x.SupplierName=m.SupplierName.Trim();x.PurchaseDate=m.PurchaseDate.Date;x.DueDate=m.DueDate.Date;x.ItemsJson=JsonSerializer.Serialize(m.Items);x.PaymentMethod=(int)m.PaymentMethod;x.Reference=m.Reference.Trim();x.Notes=m.Notes.Trim();x.Status=(int)m.Status;x.PaidAmount=m.PaidAmount;x.SyncStatus=(int)RecordSyncStatus.Pending;x.SyncError=null;x.UpdatedAtUtc=now;}
    private static object Payload(LocalPurchaseEntity x)=>new{x.Id,x.CompanyId,x.PurchaseNumber,x.SupplierId,x.SupplierName,x.PurchaseDate,x.DueDate,x.ItemsJson,x.PaymentMethod,x.Reference,x.Notes,x.Status,x.PaidAmount};
    private static SyncStatus ToUi(int s)=>(RecordSyncStatus)s==RecordSyncStatus.Synced?SyncStatus.Synced:(RecordSyncStatus)s==RecordSyncStatus.Failed?SyncStatus.SyncFailed:SyncStatus.PendingSync;
}
