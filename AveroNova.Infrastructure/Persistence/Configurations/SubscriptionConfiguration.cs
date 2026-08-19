using AveroNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AveroNova.Infrastructure.Persistence.Configurations
{
    public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
    {
        public void Configure(EntityTypeBuilder<Subscription> builder)
        {
            builder.ToTable("Subscriptions");

            builder.HasKey(x => x.Id);

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

            builder.Property(x => x.Status)
                   .HasConversion<int>()
                   .IsRequired();

            builder.Property(x => x.Plan)
                   .HasConversion<int>()
                   .IsRequired();

            builder.Property(x => x.SubscriptionType)
                   .HasConversion<int>()
                   .IsRequired();

            builder.HasOne(x => x.Company)
                   .WithMany(x => x.Subscriptions)
                   .HasForeignKey(x => x.CompanyId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => x.CompanyId);
            builder.HasIndex(x => x.PlanId);
        }
    }
}
