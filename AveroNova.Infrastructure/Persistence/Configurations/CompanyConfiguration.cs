using AveroNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace AveroNova.Infrastructure.Persistence.Configurations
{
    public class CompanyConfiguration : IEntityTypeConfiguration<Company>
    {
        public void Configure(EntityTypeBuilder<Company> builder)
        {
            builder.ToTable("Companies");

            // Primary Key
            builder.HasKey(x => x.Id);

            // Properties
            builder.Property(x => x.CompanyCode)
                   .IsRequired()
                   .HasMaxLength(50);

            builder.HasIndex(x => x.CompanyCode)
                   .IsUnique();

            builder.Property(x => x.CompanyName)
                   .IsRequired()
                   .HasMaxLength(200);

            builder.Property(x => x.OwnerName)
                   .IsRequired()
                   .HasMaxLength(150);

            builder.Property(x => x.GSTNumber)
                   .HasMaxLength(20);

            builder.Property(x => x.PANNumber)
                   .HasMaxLength(20);

            builder.Property(x => x.Email)
                   .IsRequired()
                   .HasMaxLength(150);

            builder.Property(x => x.MobileNumber)
                   .IsRequired()
                   .HasMaxLength(15);

            builder.Property(x => x.Address)
                   .HasMaxLength(500);

            builder.Property(x => x.City)
                   .HasMaxLength(100);

            builder.Property(x => x.State)
                   .HasMaxLength(100);

            builder.Property(x => x.Country)
                   .HasMaxLength(100);

            builder.Property(x => x.PinCode)
                   .HasMaxLength(10);
            builder.Property(x => x.IsDeleted);

            // Relationship : Company -> User (Many Companies, One User)
            builder.HasOne(x => x.User)
                   .WithMany(x => x.Companies)
                   .HasForeignKey(x => x.UserId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
