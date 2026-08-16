using AveroNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace AveroNova.Infrastructure.Persistence.Configurations
{
    public class RoleConfiguration : IEntityTypeConfiguration<Role>
    {
        public void Configure(EntityTypeBuilder<Role> builder)
        {
            builder.ToTable("Roles");

            // Primary Key
            builder.HasKey(x => x.Id);

            // Properties
            builder.Property(x => x.Name)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.HasIndex(x => x.Name)
                   .IsUnique();

            builder.Property(x => x.Description)
                   .HasMaxLength(500);

            // BaseEntity Properties
            builder.Property(x => x.CreatedAt)
                   .IsRequired();

            builder.Property(x => x.UpdatedAt);

            builder.Property(x => x.IsDeleted)
                   .HasDefaultValue(false);

            // Relationships
            builder.HasMany(x => x.UserRoles)
                   .WithOne(x => x.Role)
                   .HasForeignKey(x => x.RoleId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
