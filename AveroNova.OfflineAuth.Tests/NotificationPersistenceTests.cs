using AveroNova.App.UI.Data;
using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

public sealed class NotificationPersistenceTests : IAsyncLifetime
{
    private string _path = null!;
    private IDbContextFactory<LocalAppDbContext> _factory = null!;
    private LocalNotificationService _service = null!;
    private AppSessionContext _session = null!;
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _path = Path.Combine(Path.GetTempPath(), $"averonova-notifications-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<LocalAppDbContext>()
            .UseSqlite($"Data Source={_path}")
            .Options;
        _factory = new Factory(options);
        await using (var db = await _factory.CreateDbContextAsync())
            await db.Database.EnsureCreatedAsync();

        _session = new AppSessionContext();
        _session.SetFromLocal(
            new LocalUserEntity { Id = _userId, FullName = "Notification User", Email = "notify@test.local" },
            new LocalCompanyEntity { Id = _companyId, CompanyName = "Notification Co" },
            ["Owner"],
            ["notifications.manage"],
            Guid.NewGuid());
        _service = new LocalNotificationService(_factory, _session);
    }

    public Task DisposeAsync()
    {
        try { if (File.Exists(_path)) File.Delete(_path); } catch { }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Notification_PersistsMarksReadAndDeletes()
    {
        var model = new NotificationModel
        {
            Title = "Invoice overdue",
            Message = "INV-100 is overdue.",
            Category = NotificationCategory.Invoice,
            ActionRoute = "billing/view"
        };

        var created = await _service.CreateAsync(model);
        Assert.True(created.Ok, created.Error);
        Assert.Equal(1, _service.UnreadCount);

        var items = await _service.GetAllAsync();
        Assert.Single(items);
        Assert.Equal(_companyId, items[0].CompanyId);
        Assert.False(items[0].IsRead);

        await _service.MarkAsReadAsync(items[0].Id);
        var read = Assert.Single(await _service.GetAllAsync());
        Assert.True(read.IsRead);
        Assert.NotNull(read.ReadAt);
        Assert.Equal(0, _service.UnreadCount);

        await _service.DeleteAsync(read.Id);
        Assert.Empty(await _service.GetAllAsync());
    }

    [Fact]
    public async Task Notification_EnforcesCompanyAndUserIsolation()
    {
        var otherCompany = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        await using (var db = await _factory.CreateDbContextAsync())
        {
            db.Notifications.AddRange(
                new LocalNotificationEntity { CompanyId = _companyId, UserId = _userId, Title = "Visible", Message = "Visible", Category = 3 },
                new LocalNotificationEntity { CompanyId = _companyId, UserId = otherUser, Title = "Private", Message = "Hidden", Category = 3 },
                new LocalNotificationEntity { CompanyId = otherCompany, Title = "Other company", Message = "Hidden", Category = 3 });
            await db.SaveChangesAsync();
        }

        var item = Assert.Single(await _service.GetAllAsync());
        Assert.Equal("Visible", item.Title);
    }

    [Fact]
    public async Task Notification_ValidatesRequiredFieldsAndTargetUser()
    {
        var missingTitle = await _service.CreateAsync(new NotificationModel
        {
            Message = "Message",
            Category = NotificationCategory.System
        });
        Assert.False(missingTitle.Ok);

        var otherUser = await _service.CreateAsync(new NotificationModel
        {
            UserId = Guid.NewGuid(),
            Title = "Private",
            Message = "Message",
            Category = NotificationCategory.System
        });
        Assert.False(otherUser.Ok);
        Assert.Empty(await _service.GetAllAsync());
    }

    private sealed class Factory(DbContextOptions<LocalAppDbContext> options)
        : IDbContextFactory<LocalAppDbContext>
    {
        public LocalAppDbContext CreateDbContext() => new(options);
        public Task<LocalAppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }
}
