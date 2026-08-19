using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services.Mock;

public class MockNotificationService : INotificationService
{
    private readonly List<NotificationModel> _notifications = new()
    {
        new() { Title = "Invoice Overdue",    Message = "INV-2026-003 from BuildRight Inc. is overdue by 5 days.",
                Category = NotificationCategory.Invoice,    CreatedAt = DateTime.UtcNow.AddHours(-2),  IsRead = false },
        new() { Title = "Low Stock Alert",    Message = "Wireless Mouse stock is critically low (2 units).",
                Category = NotificationCategory.Inventory,  CreatedAt = DateTime.UtcNow.AddHours(-5),  IsRead = false },
        new() { Title = "Payment Received",   Message = "John Smith paid $1,250.00 for INV-2026-002.",
                Category = NotificationCategory.Payment,    CreatedAt = DateTime.UtcNow.AddDays(-1),   IsRead = true },
        new() { Title = "Sync Completed",     Message = "12 records synced successfully.",
                Category = NotificationCategory.Sync,       CreatedAt = DateTime.UtcNow.AddHours(-1),  IsRead = true },
        new() { Title = "Free Trial", Message = "Your Free Trial is active for the current company.",
                Category = NotificationCategory.Subscription, CreatedAt = DateTime.UtcNow.AddDays(-2), IsRead = false },
    };

    public int UnreadCount => _notifications.Count(n => !n.IsRead);
    public event EventHandler<int>? UnreadCountChanged;

    public Task<List<NotificationModel>> GetAllAsync()
        => Task.FromResult(_notifications.OrderByDescending(n => n.CreatedAt).ToList());

    public Task MarkAsReadAsync(Guid id)
    {
        var n = _notifications.FirstOrDefault(x => x.Id == id);
        if (n != null) n.IsRead = true;
        UnreadCountChanged?.Invoke(this, UnreadCount);
        return Task.CompletedTask;
    }

    public Task MarkAllReadAsync()
    {
        foreach (var n in _notifications) n.IsRead = true;
        UnreadCountChanged?.Invoke(this, 0);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        var n = _notifications.FirstOrDefault(x => x.Id == id);
        if (n != null) _notifications.Remove(n);
        UnreadCountChanged?.Invoke(this, UnreadCount);
        return Task.CompletedTask;
    }
}
