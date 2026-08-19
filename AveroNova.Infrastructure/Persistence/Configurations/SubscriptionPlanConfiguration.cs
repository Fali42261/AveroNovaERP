using AveroNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AveroNova.Infrastructure.Persistence.Configurations
{
    public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
    {
        public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
        {
            builder.ToTable("SubscriptionPlans");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Code)
                   .IsRequired()
                   .HasMaxLength(50);

            builder.HasIndex(x => x.Code)
                   .IsUnique();

            builder.Property(x => x.Name)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(x => x.Description)
                   .HasMaxLength(500);

            builder.Property(x => x.DurationInDays)
                   .IsRequired();

            builder.Property(x => x.Price)
                   .HasPrecision(18, 2);

            builder.HasMany(x => x.Features)
                   .WithOne(x => x.Plan)
                   .HasForeignKey(x => x.PlanId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.Subscriptions)
                   .WithOne(x => x.SubscriptionPlan)
                   .HasForeignKey(x => x.PlanId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
