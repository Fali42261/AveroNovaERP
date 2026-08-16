using AveroNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace AveroNova.Infrastructure.Persistence.Configurations
{
    public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
    {
        public void Configure(EntityTypeBuilder<Subscription> builder)
        {
            builder.ToTable("Subscriptions");

            // Primary Key
            builder.HasKey(x => x.Id);

            // Properties
            builder.Property(x => x.PlanName)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(x => x.Price)
                   .HasPrecision(18, 2)
                   .IsRequired();

            builder.Property(x => x.DurationInDays)
                   .IsRequired();

            builder.Property(x => x.StartDate)
                   .IsRequired();

            builder.Property(x => x.ExpiryDate)
                   .IsRequired();

            builder.Property(x => x.IsSubscription);
                  // .HasDefaultValue(true);

            builder.Property(x => x.Status)
                   .HasConversion<int>()     // Store enum as int
                   .IsRequired();

            builder.Property(x => x.Plan)
                   .HasConversion<int>()     // Store enum as int
                   .IsRequired();

            // Relationship
            builder.HasOne(x => x.Company)
                   .WithMany(x => x.Subscriptions)
                   .HasForeignKey(x => x.CompanyId)
                   .OnDelete(DeleteBehavior.Cascade);
        }

    }
}
