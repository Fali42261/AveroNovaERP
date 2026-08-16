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

            builder.Property(x => x.CompanyId)
                   .IsRequired();

            builder.Property(x => x.PlanId)
                   .IsRequired();

            builder.Property(x => x.StartDate)
                   .IsRequired();

            builder.Property(x => x.EndDate)
                   .IsRequired();

            builder.Property(x => x.IsTrial)
                   .IsRequired();

            builder.Property(x => x.CreditLimit)
                   .IsRequired();

            builder.Property(x => x.CreditsUsed)
                   .HasDefaultValue(0)
                   .IsRequired();

            builder.Property(x => x.Status)
                   .HasConversion<int>()
                   .IsRequired();

            builder.Ignore(x => x.RemainingCredits);

            builder.HasIndex(x => x.CompanyId);
            builder.HasIndex(x => x.PlanId);

            builder.HasOne(x => x.Plan)
                   .WithMany(x => x.Subscriptions)
                   .HasForeignKey(x => x.PlanId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Company)
                   .WithMany(x => x.Subscriptions)
                   .HasForeignKey(x => x.CompanyId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
