using AveroNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace AveroNova.Infrastructure.Persistence.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users");

            // Primary Key
            builder.HasKey(x => x.Id);

            // Properties
            builder.Property(x => x.UserCode)
                   .IsRequired()
                   .HasMaxLength(20);

            builder.HasIndex(x => x.UserCode)
                   .IsUnique();

            builder.Property(x => x.FullName)
                   .IsRequired()
                   .HasMaxLength(150);

            builder.Property(x => x.Email)
                   .IsRequired()
                   .HasMaxLength(150);

            builder.HasIndex(x => x.Email)
                   .IsUnique();

            builder.Property(x => x.MobileNumber)
                   .HasMaxLength(15);

            builder.Property(x => x.PasswordHash)
                   .IsRequired();

            builder.Property(x => x.UserImg)
                   .HasMaxLength(500);

            builder.Property(x => x.IsActiveUser)
                   .HasDefaultValue(true);

            builder.Property(x => x.IsDeleted)
                   .HasDefaultValue(false);

            // Relationships

            builder.HasMany(x => x.UserRoles)
                   .WithOne(x => x.User)
                   .HasForeignKey(x => x.UserId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.Companies)
                   .WithOne(x => x.User)
                   .HasForeignKey(x => x.UserId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
