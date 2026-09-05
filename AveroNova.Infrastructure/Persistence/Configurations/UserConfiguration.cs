using AveroNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AveroNova.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserCode).IsRequired().HasMaxLength(20);
        builder.HasIndex(x => x.UserCode).IsUnique();

        builder.Property(x => x.FullName).IsRequired().HasMaxLength(150);

        builder.Property(x => x.Email).IsRequired().HasMaxLength(150);
        builder.HasIndex(x => x.Email).IsUnique();

        builder.Property(x => x.MobileNumber).IsRequired().HasMaxLength(20);
        builder.HasIndex(x => x.MobileNumber).IsUnique();

        builder.Property(x => x.PasswordHash).IsRequired();
        builder.Property(x => x.UserImg).HasMaxLength(500);
        builder.Property(x => x.IsActiveUser).HasDefaultValue(true);

        builder.Property(x => x.SyncStatus).HasConversion<int>();
        builder.Property(x => x.SyncVersion).HasDefaultValue(1L);

        builder.HasMany(x => x.UserRoles)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.UserCompanies)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.DeviceSessions)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
