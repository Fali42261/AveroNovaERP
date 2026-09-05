using System.Text.Json;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace AveroNova.App.UI.Services;

/// <summary>
/// Persistent local notification inbox. Unlike the old mock service this never seeds
/// demo data. Notifications are stored in the app data directory and survive restarts.
/// </summary>
public sealed class LocalNotificationService : INotificationService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<LocalNotificationService> _logger;
    private readonly string _filePath;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private List<NotificationModel>? _items;

    public LocalNotificationService(ILogger<LocalNotificationService> logger)
    {
        _logger = logger;
        _filePath = Path.Combine(FileSystem.AppDataDirectory, "notifications.json");
    }

    public int UnreadCount => _items?.Count(x => !x.IsRead) ?? 0;

    public event EventHandler<int>? UnreadCountChanged;

    public async Task<List<NotificationModel>> GetAllAsync()
    {
        await EnsureLoadedAsync();
        return _items!
            .OrderByDescending(x => x.CreatedAt)
            .Select(Clone)
            .ToList();
    }

    public async Task MarkAsReadAsync(Guid id)
    {
        await MutateAsync(items =>
        {
            var item = items.FirstOrDefault(x => x.Id == id);
            if (item != null)
                item.IsRead = true;
        });
    }

    public Task MarkAllReadAsync()
        => MutateAsync(items =>
        {
            foreach (var item in items)
                item.IsRead = true;
        });

    public Task DeleteAsync(Guid id)
        => MutateAsync(items => items.RemoveAll(x => x.Id == id));

    /// <summary>
    /// Production publisher entry point for app/domain services that need to add an inbox item.
    /// </summary>
    public Task PublishAsync(
        string title,
        string message,
        NotificationCategory category,
        string? actionRoute = null)
        => MutateAsync(items => items.Add(new NotificationModel
        {
            Id = Guid.NewGuid(),
            Title = title?.Trim() ?? string.Empty,
            Message = message?.Trim() ?? string.Empty,
            Category = category,
            CreatedAt = DateTime.UtcNow,
            IsRead = false,
            ActionRoute = string.IsNullOrWhiteSpace(actionRoute) ? null : actionRoute.Trim()
        }));

    private async Task EnsureLoadedAsync()
    {
        if (_items != null)
            return;

        await _gate.WaitAsync();
        try
        {
            if (_items != null)
                return;

            _items = await ReadAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task MutateAsync(Action<List<NotificationModel>> mutation)
    {
        await _gate.WaitAsync();
        try
        {
            _items ??= await ReadAsync();
            var before = _items.Count(x => !x.IsRead);
            mutation(_items);
            await WriteAsync(_items);
            var after = _items.Count(x => !x.IsRead);
            if (before != after)
                UnreadCountChanged?.Invoke(this, after);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<NotificationModel>> ReadAsync()
    {
        try
        {
            if (!File.Exists(_filePath))
                return [];

            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<List<NotificationModel>>(stream, _json) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to read local notifications from {Path}.", _filePath);
            return [];
        }
    }

    private async Task WriteAsync(List<NotificationModel> items)
    {
        var tempPath = _filePath + ".tmp";
        try
        {
            await using (var stream = File.Create(tempPath))
                await JsonSerializer.SerializeAsync(stream, items, _json);

            File.Move(tempPath, _filePath, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to persist local notifications to {Path}.", _filePath);
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // Best effort cleanup only.
            }
            throw;
        }
    }

    private static NotificationModel Clone(NotificationModel value) => new()
    {
        Id = value.Id,
        Title = value.Title,
        Message = value.Message,
        Category = value.Category,
        CreatedAt = value.CreatedAt,
        IsRead = value.IsRead,
        ActionRoute = value.ActionRoute
    };
}
