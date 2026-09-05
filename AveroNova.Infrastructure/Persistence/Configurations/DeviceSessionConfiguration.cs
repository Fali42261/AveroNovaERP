using AveroNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AveroNova.Infrastructure.Persistence.Configurations;

public class DeviceSessionConfiguration : IEntityTypeConfiguration<DeviceSession>
{
    public void Configure(EntityTypeBuilder<DeviceSession> builder)
    {
        builder.ToTable("DeviceSessions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DeviceId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.DeviceName).HasMaxLength(200);
        builder.Property(x => x.Platform).HasMaxLength(64);
        builder.Property(x => x.RefreshTokenHash).IsRequired().HasMaxLength(512);
        builder.Property(x => x.TokenFamilyId).HasMaxLength(64);
        builder.HasIndex(x => x.RefreshTokenHash);
        builder.HasIndex(x => new { x.UserId, x.DeviceId });

        builder.Property(x => x.SyncStatus).HasConversion<int>();
        builder.Property(x => x.SyncVersion).HasDefaultValue(1L);

        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
