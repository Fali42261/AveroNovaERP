namespace AveroNova.Domain.Enums;

/// <summary>
/// Canonical record-level sync state for Offline-First entities (server + local).
/// </summary>
public enum RecordSyncStatus
{
    Pending = 0,
    Syncing = 1,
    Synced = 2,
    Failed = 3,
    Conflict = 4,
    Deleted = 5
}

public enum SyncOperation
{
    Create = 1,
    Update = 2,
    Delete = 3
}

public enum ConflictResolutionPolicy
{
    DetectAndRetainBoth = 0,
    ServerWins = 1,
    ClientWins = 2
}
