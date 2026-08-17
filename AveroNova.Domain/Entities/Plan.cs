using System.Collections.Generic;
using AveroNova.Domain.Constants;

namespace AveroNova.Domain.Entities;

public class Plan : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TrialDays { get; set; }
    public int CreditLimit { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "INR";
    public bool IsAvailable { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();

    public DateTime CalculatePeriodEndDate(DateTime startDateUtc)
        => startDateUtc.AddDays(TrialDays > 0 ? TrialDays : 30);

    public static Plan CreateStarterCatalog()
        => new()
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = PlanNames.Starter,
            Description = "Starter plan with 15-day free trial.",
            TrialDays = 15,
            CreditLimit = 1000,
            Price = 0m,
            Currency = "INR",
            IsAvailable = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            SyncStatus = Enums.RecordSyncStatus.Synced
        };

    public static Plan CreateBusinessCatalog()
        => new()
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = PlanNames.Business,
            Description = "Business plan — Coming Soon.",
            TrialDays = 0,
            CreditLimit = 10000,
            Price = 0m,
            Currency = "INR",
            IsAvailable = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            SyncStatus = Enums.RecordSyncStatus.Synced
        };

    public static Plan CreateEnterpriseCatalog()
        => new()
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Name = PlanNames.Enterprise,
            Description = "Enterprise plan — Coming Soon.",
            TrialDays = 0,
            CreditLimit = -1,
            Price = 0m,
            Currency = "INR",
            IsAvailable = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            SyncStatus = Enums.RecordSyncStatus.Synced
        };
}
