using AveroNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AveroNova.Infrastructure.Persistence.Configurations;

public class SyncQueueItemConfiguration : IEntityTypeConfiguration<SyncQueueItem>
{
    public void Configure(EntityTypeBuilder<SyncQueueItem> builder)
    {
        builder.ToTable("SyncQueue");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntityType).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Operation).HasConversion<int>();
        builder.Property(x => x.QueueStatus).HasConversion<int>();
        builder.Property(x => x.Error).HasMaxLength(2000);
        builder.HasIndex(x => new { x.QueueStatus, x.CreatedAt });
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.Property(x => x.SyncStatus).HasConversion<int>();
        builder.Property(x => x.SyncVersion).HasDefaultValue(1L);
    }
}
