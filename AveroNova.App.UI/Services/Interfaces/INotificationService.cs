using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

public interface INotificationService
{
    Task<List<NotificationModel>> GetAllAsync();
    Task                          MarkAsReadAsync(Guid id);
    Task                          MarkAllReadAsync();
    Task                          DeleteAsync(Guid id);
    int                           UnreadCount { get; }
    event EventHandler<int>       UnreadCountChanged;
}
