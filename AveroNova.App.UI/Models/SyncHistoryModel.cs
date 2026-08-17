namespace AveroNova.App.UI.Models;

public class SyncHistoryModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    public bool Success { get; set; }
    public int ItemsSynced { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
}
