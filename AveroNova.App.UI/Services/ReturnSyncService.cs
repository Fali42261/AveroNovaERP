using System.Text.Json;
using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Api;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Services.Security;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AveroNova.App.UI.Services;

public interface IReturnSyncService
{
    Task SyncPendingAsync(CancellationToken cancellationToken = default);
}

public sealed class ReturnSyncService : IReturnSyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IDbContextFactory<LocalAppDbContext> _dbFactory;
    private readonly IApiClient _api;
    private readonly ISecureTokenStore _tokens;
    private readonly IConnectivityService _connectivity;
    private readonly ILogger<ReturnSyncService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ReturnSyncService(IDbContextFactory<LocalAppDbContext> dbFactory, IApiClient api, ISecureTokenStore tokens, IConnectivityService connectivity, ILogger<ReturnSyncService> logger)
    {
        _dbFactory=dbFactory; _api=api; _tokens=tokens; _connectivity=connectivity; _logger=logger;
        _connectivity.StatusChanged += (_, status) => { if (status is ConnectivityStatus.Online or ConnectivityStatus.Synced or ConnectivityStatus.PendingSync) _ = SyncPendingAsync(); };
    }

    public async Task SyncPendingAsync(CancellationToken cancellationToken = default)
    {
        if (!_connectivity.IsOnline || !await _gate.WaitAsync(0, cancellationToken)) return;
        try
        {
            var token = await _tokens.GetAccessTokenAsync();
            if (string.IsNullOrWhiteSpace(token)) return;
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var items = await db.SyncQueue.Where(x => (x.EntityType == "SalesReturn" || x.EntityType == "PurchaseReturn") &&
                (x.Status == (int)RecordSyncStatus.Pending || x.Status == (int)RecordSyncStatus.Failed)).OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);

            foreach (var item in items)
            {
                item.Status=(int)RecordSyncStatus.Syncing; item.LastAttemptAt=DateTime.UtcNow; await db.SaveChangesAsync(cancellationToken);
                var op=(SyncOperation)item.Operation;
                var route=item.EntityType=="SalesReturn"?"api/returns/sales":"api/returns/purchase";
                ApiCallResult result;
                if(op==SyncOperation.Delete)
                {
                    result=await _api.DeleteAsync($"{route}/{item.EntityId:D}",token,cancellationToken);
                }
                else
                {
                    ReturnSyncPayload? payload;
                    try { payload=string.IsNullOrWhiteSpace(item.PayloadJson)?null:JsonSerializer.Deserialize<ReturnSyncPayload>(item.PayloadJson,JsonOptions); }
                    catch { payload=null; }
                    if(payload is null || payload.Id==Guid.Empty || payload.CompanyId==Guid.Empty || string.IsNullOrWhiteSpace(payload.ReturnNumber))
                    {
                        MarkFailed(item,"Return sync payload is incomplete."); await db.SaveChangesAsync(cancellationToken); continue;
                    }
                    ApiCallResult<ReturnSyncResponse> typed=op switch
                    {
                        SyncOperation.Create=>await _api.PostAsync<ReturnSyncResponse>(route,payload,token,cancellationToken),
                        SyncOperation.Update=>await _api.PutAsync<ReturnSyncResponse>($"{route}/{payload.Id:D}",payload,token,cancellationToken),
                        _=>ApiCallResult<ReturnSyncResponse>.Fail(400,"Unsupported return sync operation.")
                    };
                    result=typed;
                    if(typed.Success&&typed.Data is not null)
                    {
                        if(item.EntityType=="SalesReturn")
                        {
                            var row=await db.SalesReturns.FirstOrDefaultAsync(x=>x.Id==payload.Id,cancellationToken);
                            if(row is not null){row.ServerId=typed.Data.Id;row.SyncStatus=(int)RecordSyncStatus.Synced;row.LastSyncedAtUtc=DateTime.UtcNow;row.SyncError=null;}
                        }
                        else
                        {
                            var row=await db.PurchaseReturns.FirstOrDefaultAsync(x=>x.Id==payload.Id,cancellationToken);
                            if(row is not null){row.ServerId=typed.Data.Id;row.SyncStatus=(int)RecordSyncStatus.Synced;row.LastSyncedAtUtc=DateTime.UtcNow;row.SyncError=null;}
                        }
                    }
                }

                if(!result.Success)
                {
                    item.RetryCount++;item.Error=result.Error??"Return sync failed.";item.LastAttemptAt=DateTime.UtcNow;
                    item.Status=(int)(result.IsNetworkError||item.RetryCount<5?RecordSyncStatus.Pending:RecordSyncStatus.Failed);
                    await db.SaveChangesAsync(cancellationToken);continue;
                }
                item.Status=(int)RecordSyncStatus.Synced;item.Error=null;item.SyncedAt=DateTime.UtcNow;item.LastAttemptAt=DateTime.UtcNow;
                _connectivity.DecrementPending();await db.SaveChangesAsync(cancellationToken);
            }
        }
        catch(Exception ex){_logger.LogWarning(ex,"Return sync failed; local data remains queued.");}
        finally{_gate.Release();}
    }

    private static void MarkFailed(LocalSyncQueueEntity item,string error){item.RetryCount++;item.Error=error;item.LastAttemptAt=DateTime.UtcNow;item.Status=(int)RecordSyncStatus.Failed;}

    private sealed class ReturnSyncPayload
    {
        public Guid Id { get; set; } public Guid CompanyId { get; set; } public string ReturnNumber { get; set; }=string.Empty;
        public Guid InvoiceId { get; set; } public string InvoiceNumber { get; set; }=string.Empty; public Guid CustomerId { get; set; } public string CustomerName { get; set; }=string.Empty;
        public Guid PurchaseId { get; set; } public string PurchaseNumber { get; set; }=string.Empty; public Guid SupplierId { get; set; } public string SupplierName { get; set; }=string.Empty;
        public DateTime ReturnDate { get; set; } public string ItemsJson { get; set; }="[]"; public string Reason { get; set; }=string.Empty; public string Notes { get; set; }=string.Empty;
        public decimal RefundAmount { get; set; } public int Status { get; set; } public long SyncVersion { get; set; }
    }
    private sealed class ReturnSyncResponse{public Guid Id{get;set;}public long SyncVersion{get;set;}public DateTime? UpdatedAt{get;set;}}
}
