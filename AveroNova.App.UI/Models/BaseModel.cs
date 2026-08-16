using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AveroNova.App.UI.Models;

// ═══════════════════════════════════════════════════════════════
//  AVERONOVA ERP — BASE MODEL
//  All domain models inherit from this to get:
//  - Unique local ID
//  - Server ID (null until synced)
//  - Sync tracking fields
//  - INotifyPropertyChanged
// ═══════════════════════════════════════════════════════════════

public abstract class BaseModel : INotifyPropertyChanged
{
    private SyncStatus _syncStatus = SyncStatus.Local;

    /// <summary>Local-only unique identifier (Guid).</summary>
    public Guid LocalId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Server-assigned ID.
    /// Null until the record has been successfully synced with the API.
    /// </summary>
    public string? ServerId { get; set; }

    /// <summary>UTC timestamp of the last successful sync.</summary>
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>UTC creation timestamp (local device time).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC last-modified timestamp (local device time).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Sync state of this record.
    /// Drives offline-first UI indicators and sync queue logic.
    /// </summary>
    public SyncStatus SyncStatus
    {
        get => _syncStatus;
        set { _syncStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsPendingSync)); }
    }

    /// <summary>True if this record is waiting to be uploaded.</summary>
    public bool IsPendingSync => SyncStatus is SyncStatus.PendingSync or SyncStatus.Local;

    // ── INotifyPropertyChanged ─────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
