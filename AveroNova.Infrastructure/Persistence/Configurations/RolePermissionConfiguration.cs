using AveroNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AveroNova.Infrastructure.Persistence.Configurations;

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");

        // Primary Key
        builder.HasKey(x => x.Id);

        // Unique Constraint
        builder.HasIndex(x => new { x.RoleId, x.PermissionId })
               .IsUnique();

        // BaseEntity Properties
        builder.Property(x => x.CreatedAt)
               .IsRequired();

        builder.Property(x => x.UpdatedAt);

        builder.Property(x => x.IsDeleted)
               .HasDefaultValue(false);

        // Relationships
        builder.HasOne(x => x.Role)
               .WithMany(x => x.RolePermissions)
               .HasForeignKey(x => x.RoleId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Permission)
               .WithMany(x => x.RolePermissions)
               .HasForeignKey(x => x.PermissionId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}