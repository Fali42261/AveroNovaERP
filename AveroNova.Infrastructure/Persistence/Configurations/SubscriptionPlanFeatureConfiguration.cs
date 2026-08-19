using AveroNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AveroNova.Infrastructure.Persistence.Configurations
{
    public class SubscriptionPlanFeatureConfiguration : IEntityTypeConfiguration<SubscriptionPlanFeature>
    {
        public void Configure(EntityTypeBuilder<SubscriptionPlanFeature> builder)
        {
            builder.ToTable("SubscriptionPlanFeatures");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.ModuleKey)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.Property(x => x.ModuleName)
                   .IsRequired()
                   .HasMaxLength(150);

            builder.HasIndex(x => new { x.PlanId, x.ModuleKey })
                   .IsUnique();
        }
    }
}
