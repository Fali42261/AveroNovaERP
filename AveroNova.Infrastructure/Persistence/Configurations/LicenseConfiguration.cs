using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AveroNova.Infrastructure.Persistence.Configurations;

public sealed class LicenseConfiguration : IEntityTypeConfiguration<License>
{
    public void Configure(EntityTypeBuilder<License> builder)
    {
        builder.ToTable("Licenses");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DeviceId).IsRequired().HasMaxLength(128);
        builder.HasIndex(x => x.DeviceId).IsUnique();

        builder.Property(x => x.Plan).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.SyncStatus).HasConversion<int>();
        builder.Property(x => x.SyncVersion).HasDefaultValue(1L);

        builder.Property(x => x.TrialStartDateUtc).IsRequired();
        builder.Property(x => x.TrialEndDateUtc).IsRequired();
        builder.Property(x => x.StartDateUtc).IsRequired();

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.CompanyId);

        builder.HasOne(x => x.User)
            .WithMany(x => x.Licenses)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(x => x.Company)
            .WithMany(x => x.Licenses)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
