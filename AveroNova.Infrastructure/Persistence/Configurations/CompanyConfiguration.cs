using AveroNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AveroNova.Infrastructure.Persistence.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CompanyCode).IsRequired().HasMaxLength(50);
        builder.HasIndex(x => x.CompanyCode).IsUnique();

        builder.Property(x => x.CompanyName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.OwnerName).HasMaxLength(150);
        builder.Property(x => x.GSTNumber).HasMaxLength(20);
        builder.Property(x => x.PANNumber).HasMaxLength(20);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(150);
        builder.Property(x => x.MobileNumber).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.State).HasMaxLength(100);
        builder.Property(x => x.Country).HasMaxLength(100);
        builder.Property(x => x.PinCode).HasMaxLength(10);
        builder.Property(x => x.IsActive).HasDefaultValue(true);
        builder.Property(x => x.SyncStatus).HasConversion<int>();
        builder.Property(x => x.SyncVersion).HasDefaultValue(1L);

        builder.HasMany(x => x.Subscriptions)
            .WithOne(x => x.Company)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.UserCompanies)
            .WithOne(x => x.Company)
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
