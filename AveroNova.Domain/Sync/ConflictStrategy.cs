namespace AveroNova.Domain.Sync;

/// <summary>
/// Offline-First conflict strategy foundation.
/// Detect conflicts; do not silently overwrite. ERP modules may refine rules later.
/// </summary>
public static class ConflictStrategy
{
    public const Enums.ConflictResolutionPolicy DefaultPolicy =
        Enums.ConflictResolutionPolicy.DetectAndRetainBoth;

    public static bool IsConflict(long localVersion, long serverVersion, long lastSyncedVersion)
        => localVersion > lastSyncedVersion && serverVersion > lastSyncedVersion;
}

/// <summary>
/// Idempotency: client-generated GUIDs are stable identity. Retries must not create duplicates.
/// </summary>
public static class SyncIdempotency
{
    public const string Strategy =
        "Stable GUID primary keys. Create is idempotent by Id. " +
        "Update is versioned by SyncVersion. " +
        "Duplicate posts of the same Id must not create duplicates.";
}
