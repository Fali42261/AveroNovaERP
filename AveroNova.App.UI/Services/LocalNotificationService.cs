using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services;

public sealed class LocalNotificationService : INotificationService
{
    private readonly IDbContextFactory<LocalAppDbContext> _factory;
    private readonly IAppSessionContext _session;

    public LocalNotificationService(
        IDbContextFactory<LocalAppDbContext> factory,
        IAppSessionContext session)
    {
        _factory = factory;
        _session = session;
    }

    public int UnreadCount { get; private set; }
    public event EventHandler<int>? UnreadCountChanged;

    public async Task<List<NotificationModel>> GetAllAsync()
    {
        if (!TryGetScope(out var companyId, out var userId))
        {
            SetUnreadCount(0);
            return [];
        }

        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.Notifications
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && (x.UserId == null || x.UserId == userId))
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        SetUnreadCount(rows.Count(x => !x.IsRead));
        return rows.Select(Map).ToList();
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(NotificationModel model)
    {
        if (!TryGetScope(out var companyId, out var userId))
            return (false, "An active company session is required.");
        if (model.UserId is Guid targetUser && targetUser != userId)
            return (false, "You cannot create a notification for another user.");
        if (string.IsNullOrWhiteSpace(model.Title))
            return (false, "Title is required.");
        if (model.Title.Trim().Length > 160)
            return (false, "Title must be 160 characters or fewer.");
        if (string.IsNullOrWhiteSpace(model.Message))
            return (false, "Message is required.");
        if (model.Message.Trim().Length > 1000)
            return (false, "Message must be 1000 characters or fewer.");
        if (!Enum.IsDefined(model.Category))
            return (false, "Notification category is invalid.");
        if (model.ActionRoute?.Trim().Length > 256)
            return (false, "Action route must be 256 characters or fewer.");

        var row = new LocalNotificationEntity
        {
            Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id,
            CompanyId = companyId,
            UserId = model.UserId,
            Title = model.Title.Trim(),
            Message = model.Message.Trim(),
            Category = (int)model.Category,
            CreatedAtUtc = model.CreatedAt == default ? DateTime.UtcNow : model.CreatedAt.ToUniversalTime(),
            IsRead = model.IsRead,
            ReadAtUtc = model.IsRead ? model.ReadAt ?? DateTime.UtcNow : null,
            ActionRoute = string.IsNullOrWhiteSpace(model.ActionRoute) ? null : model.ActionRoute.Trim()
        };

        await using var db = await _factory.CreateDbContextAsync();
        if (await db.Notifications.AnyAsync(x => x.Id == row.Id))
            return (false, "Notification already exists.");
        db.Notifications.Add(row);
        await db.SaveChangesAsync();

        model.Id = row.Id;
        model.CompanyId = companyId;
        model.ReadAt = row.ReadAtUtc;
        await RefreshUnreadCountAsync();
        return (true, null);
    }

    public async Task MarkAsReadAsync(Guid id)
    {
        if (!TryGetScope(out var companyId, out var userId)) return;
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.Notifications.FirstOrDefaultAsync(
            x => x.Id == id && x.CompanyId == companyId && (x.UserId == null || x.UserId == userId));
        if (row is null || row.IsRead) return;
        row.IsRead = true;
        row.ReadAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await RefreshUnreadCountAsync();
    }

    public async Task MarkAllReadAsync()
    {
        if (!TryGetScope(out var companyId, out var userId)) return;
        await using var db = await _factory.CreateDbContextAsync();
        var rows = await db.Notifications
            .Where(x => x.CompanyId == companyId
                        && (x.UserId == null || x.UserId == userId)
                        && !x.IsRead)
            .ToListAsync();
        if (rows.Count == 0) return;
        var now = DateTime.UtcNow;
        foreach (var row in rows)
        {
            row.IsRead = true;
            row.ReadAtUtc = now;
        }
        await db.SaveChangesAsync();
        SetUnreadCount(0);
    }

    public async Task DeleteAsync(Guid id)
    {
        if (!TryGetScope(out var companyId, out var userId)) return;
        await using var db = await _factory.CreateDbContextAsync();
        var row = await db.Notifications.FirstOrDefaultAsync(
            x => x.Id == id && x.CompanyId == companyId && (x.UserId == null || x.UserId == userId));
        if (row is null) return;
        db.Notifications.Remove(row);
        await db.SaveChangesAsync();
        await RefreshUnreadCountAsync();
    }

    private async Task RefreshUnreadCountAsync()
    {
        if (!TryGetScope(out var companyId, out var userId))
        {
            SetUnreadCount(0);
            return;
        }
        await using var db = await _factory.CreateDbContextAsync();
        SetUnreadCount(await db.Notifications.CountAsync(
            x => x.CompanyId == companyId && (x.UserId == null || x.UserId == userId) && !x.IsRead));
    }

    private bool TryGetScope(out Guid companyId, out Guid userId)
    {
        companyId = _session.CurrentCompanyId ?? Guid.Empty;
        userId = _session.CurrentUserId ?? Guid.Empty;
        return companyId != Guid.Empty && userId != Guid.Empty;
    }

    private void SetUnreadCount(int value)
    {
        if (UnreadCount == value) return;
        UnreadCount = value;
        UnreadCountChanged?.Invoke(this, value);
    }

    private static NotificationModel Map(LocalNotificationEntity row) => new()
    {
        Id = row.Id,
        CompanyId = row.CompanyId,
        UserId = row.UserId,
        Title = row.Title,
        Message = row.Message,
        Category = (NotificationCategory)row.Category,
        CreatedAt = row.CreatedAtUtc,
        IsRead = row.IsRead,
        ReadAt = row.ReadAtUtc,
        ActionRoute = row.ActionRoute
    };
}
