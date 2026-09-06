using System.Data;
using AveroNova.App.UI.Data;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services;

/// <summary>
/// Keeps server concurrency versions in the existing local SQLite tables without
/// forcing a destructive local database rebuild. This is intentionally small and
/// idempotent so upgraded installations can evolve in place.
/// </summary>
public static class LocalSyncVersionStore
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _schemaReady;

    public static async Task EnsureSchemaAsync(LocalAppDbContext db, CancellationToken cancellationToken = default)
    {
        if (_schemaReady) return;
        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (_schemaReady) return;
            await EnsureColumnAsync(db, "LocalExpenses", cancellationToken);
            await EnsureColumnAsync(db, "LocalSalesReturns", cancellationToken);
            await EnsureColumnAsync(db, "LocalPurchaseReturns", cancellationToken);
            _schemaReady = true;
        }
        finally { Gate.Release(); }
    }

    public static Task<long> GetExpenseAsync(LocalAppDbContext db, Guid id, CancellationToken cancellationToken = default)
        => GetAsync(db, "LocalExpenses", id, cancellationToken);
    public static Task<long> GetSalesReturnAsync(LocalAppDbContext db, Guid id, CancellationToken cancellationToken = default)
        => GetAsync(db, "LocalSalesReturns", id, cancellationToken);
    public static Task<long> GetPurchaseReturnAsync(LocalAppDbContext db, Guid id, CancellationToken cancellationToken = default)
        => GetAsync(db, "LocalPurchaseReturns", id, cancellationToken);

    public static Task SetExpenseAsync(LocalAppDbContext db, Guid id, long version, CancellationToken cancellationToken = default)
        => SetAsync(db, "LocalExpenses", id, version, cancellationToken);
    public static Task SetSalesReturnAsync(LocalAppDbContext db, Guid id, long version, CancellationToken cancellationToken = default)
        => SetAsync(db, "LocalSalesReturns", id, version, cancellationToken);
    public static Task SetPurchaseReturnAsync(LocalAppDbContext db, Guid id, long version, CancellationToken cancellationToken = default)
        => SetAsync(db, "LocalPurchaseReturns", id, version, cancellationToken);

    private static async Task EnsureColumnAsync(LocalAppDbContext db, string table, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);
        await using var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info([{table}]);";
        await using var reader = await check.ExecuteReaderAsync(ct);
        var exists = false;
        while (await reader.ReadAsync(ct))
        {
            if (string.Equals(reader.GetString(1), "SyncVersion", StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
        }
        await reader.DisposeAsync();
        if (exists) return;
        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE [{table}] ADD COLUMN [SyncVersion] INTEGER NOT NULL DEFAULT 1;";
        await alter.ExecuteNonQueryAsync(ct);
    }

    private static async Task<long> GetAsync(LocalAppDbContext db, string table, Guid id, CancellationToken ct)
    {
        await EnsureSchemaAsync(db, ct);
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COALESCE([SyncVersion],1) FROM [{table}] WHERE [Id]=$id LIMIT 1;";
        var parameter = command.CreateParameter(); parameter.ParameterName = "$id"; parameter.Value = id.ToString(); command.Parameters.Add(parameter);
        var value = await command.ExecuteScalarAsync(ct);
        return value is null || value is DBNull ? 1L : Convert.ToInt64(value);
    }

    private static async Task SetAsync(LocalAppDbContext db, string table, Guid id, long version, CancellationToken ct)
    {
        await EnsureSchemaAsync(db, ct);
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = $"UPDATE [{table}] SET [SyncVersion]=$version WHERE [Id]=$id;";
        var idParameter = command.CreateParameter(); idParameter.ParameterName = "$id"; idParameter.Value = id.ToString(); command.Parameters.Add(idParameter);
        var versionParameter = command.CreateParameter(); versionParameter.ParameterName = "$version"; versionParameter.Value = Math.Max(1, version); command.Parameters.Add(versionParameter);
        await command.ExecuteNonQueryAsync(ct);
    }
}
