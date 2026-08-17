using AveroNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AveroNova.Infrastructure.Persistence.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");

        // Primary Key
        builder.HasKey(x => x.Id);

        // Properties
        builder.Property(x => x.PermissionName)
               .IsRequired()
               .HasMaxLength(100);

        builder.HasIndex(x => x.PermissionName)
               .IsUnique();

        builder.Property(x => x.Description)
               .HasMaxLength(500);

        // BaseEntity Properties
        builder.Property(x => x.CreatedAt)
               .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.Property(x => x.IsDeleted)
               .HasDefaultValue(false);

        builder.Property(x => x.SyncStatus).HasConversion<int>();
        builder.Property(x => x.SyncVersion).HasDefaultValue(1L);

        // Relationships
        builder.HasMany(x => x.RolePermissions)
               .WithOne(x => x.Permission)
               .HasForeignKey(x => x.PermissionId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}