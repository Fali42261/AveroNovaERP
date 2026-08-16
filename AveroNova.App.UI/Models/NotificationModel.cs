namespace AveroNova.App.UI.Models;

public class NotificationModel
{
    public Guid                 Id          { get; set; } = Guid.NewGuid();
    public string               Title       { get; set; } = string.Empty;
    public string               Message     { get; set; } = string.Empty;
    public NotificationCategory Category    { get; set; }
    public DateTime             CreatedAt   { get; set; } = DateTime.UtcNow;
    public bool                 IsRead      { get; set; }
    public string?              ActionRoute { get; set; }

    public string TimeAgo
    {
        get
        {
            var diff = DateTime.UtcNow - CreatedAt;
            if (diff.TotalMinutes < 1)  return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24)   return $"{(int)diff.TotalHours}h ago";
            return $"{(int)diff.TotalDays}d ago";
        }
    }
}

public class SyncHistoryModel
{
    public Guid     Id         { get; set; } = Guid.NewGuid();
    public DateTime SyncedAt   { get; set; } = DateTime.UtcNow;
    public bool     Success    { get; set; }
    public int      ItemsSynced { get; set; }
    public string   Message    { get; set; } = string.Empty;
    public string   Module     { get; set; } = string.Empty;
}
