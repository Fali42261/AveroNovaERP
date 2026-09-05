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

public interface IExpenseSyncService { Task SyncPendingAsync(CancellationToken cancellationToken = default); }

public sealed class ExpenseSyncService : IExpenseSyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly IDbContextFactory<LocalAppDbContext> _dbFactory; private readonly IApiClient _api;
    private readonly ISecureTokenStore _tokens; private readonly IConnectivityService _connectivity;
    private readonly ILogger<ExpenseSyncService> _logger; private readonly SemaphoreSlim _gate = new(1,1);

    public ExpenseSyncService(IDbContextFactory<LocalAppDbContext> dbFactory, IApiClient api, ISecureTokenStore tokens,
        IConnectivityService connectivity, ILogger<ExpenseSyncService> logger)
    { _dbFactory=dbFactory;_api=api;_tokens=tokens;_connectivity=connectivity;_logger=logger;_connectivity.StatusChanged+=OnConnectivityChanged; }

    public async Task SyncPendingAsync(CancellationToken cancellationToken=default)
    {
        if(!_connectivity.IsOnline||!await _gate.WaitAsync(0,cancellationToken))return;
        try
        {
            var token=await _tokens.GetAccessTokenAsync(); if(string.IsNullOrWhiteSpace(token))return;
            await using var db=await _dbFactory.CreateDbContextAsync(cancellationToken);
            await LocalSyncVersionStore.EnsureSchemaAsync(db,cancellationToken);
            var items=await db.SyncQueue.Where(x=>x.EntityType=="Expense"&&(x.Status==(int)RecordSyncStatus.Pending||x.Status==(int)RecordSyncStatus.Failed)).OrderBy(x=>x.CreatedAt).ToListAsync(cancellationToken);
            foreach(var item in items)
            {
                item.Status=(int)RecordSyncStatus.Syncing;item.LastAttemptAt=DateTime.UtcNow;await db.SaveChangesAsync(cancellationToken);
                ApiCallResult result; var op=(SyncOperation)item.Operation;
                if(op==SyncOperation.Delete) result=await _api.DeleteAsync($"api/expenses/{item.EntityId:D}",token,cancellationToken);
                else
                {
                    ExpensePayload? payload;try{payload=JsonSerializer.Deserialize<ExpensePayload>(item.PayloadJson,JsonOptions);}catch{payload=null;}
                    if(payload is null||payload.Id==Guid.Empty||payload.CompanyId==Guid.Empty||payload.Amount<=0||string.IsNullOrWhiteSpace(payload.Category))
                    { Fail(item,"Expense sync payload is incomplete.");await db.SaveChangesAsync(cancellationToken);continue; }
                    payload.SyncVersion=await LocalSyncVersionStore.GetExpenseAsync(db,payload.Id,cancellationToken);
                    ApiCallResult<ExpenseResponse> typed=op switch
                    { SyncOperation.Create=>await _api.PostAsync<ExpenseResponse>("api/expenses",payload,token,cancellationToken),
                      SyncOperation.Update=>await _api.PutAsync<ExpenseResponse>($"api/expenses/{payload.Id:D}",payload,token,cancellationToken),
                      _=>ApiCallResult<ExpenseResponse>.Fail(400,"Unsupported expense sync operation.") };
                    result=typed;
                    if(typed.Success&&typed.Data is not null)
                    {
                        var row=await db.Expenses.FirstOrDefaultAsync(x=>x.Id==payload.Id,cancellationToken);
                        if(row is not null){row.ServerId=typed.Data.Id;row.SyncStatus=(int)RecordSyncStatus.Synced;row.LastSyncedAtUtc=DateTime.UtcNow;row.SyncError=null;}
                        await LocalSyncVersionStore.SetExpenseAsync(db,payload.Id,typed.Data.SyncVersion,cancellationToken);
                    }
                }
                if(!result.Success){item.RetryCount++;item.Error=result.Error??"Expense sync failed.";item.LastAttemptAt=DateTime.UtcNow;item.Status=(int)(result.IsNetworkError||item.RetryCount<5?RecordSyncStatus.Pending:RecordSyncStatus.Failed);await db.SaveChangesAsync(cancellationToken);continue;}
                item.Status=(int)RecordSyncStatus.Synced;item.Error=null;item.SyncedAt=DateTime.UtcNow;item.LastAttemptAt=DateTime.UtcNow;_connectivity.DecrementPending();await db.SaveChangesAsync(cancellationToken);
            }
        }
        catch(Exception ex){_logger.LogWarning(ex,"Expense sync failed; local data remains queued.");}
        finally{_gate.Release();}
    }
    private void OnConnectivityChanged(object? sender,ConnectivityStatus status){if(status is ConnectivityStatus.Online or ConnectivityStatus.Synced or ConnectivityStatus.PendingSync)_=SyncPendingAsync();}
    private static void Fail(LocalSyncQueueEntity item,string error){item.RetryCount++;item.Error=error;item.LastAttemptAt=DateTime.UtcNow;item.Status=(int)RecordSyncStatus.Failed;}
    private sealed class ExpensePayload{public Guid Id{get;set;}public Guid CompanyId{get;set;}public string Category{get;set;}=string.Empty;public string Description{get;set;}=string.Empty;public decimal Amount{get;set;}public DateTime ExpenseDate{get;set;}public int Method{get;set;}public string Reference{get;set;}=string.Empty;public string Notes{get;set;}=string.Empty;public int Status{get;set;}public string ApprovedBy{get;set;}=string.Empty;public long SyncVersion{get;set;}}
    private sealed class ExpenseResponse{public Guid Id{get;set;}public long SyncVersion{get;set;}public DateTime? UpdatedAt{get;set;}}
}
