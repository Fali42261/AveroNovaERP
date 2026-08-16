using AveroNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AveroNova.Infrastructure.Persistence.Configurations
{
    public class PlanConfiguration : IEntityTypeConfiguration<Plan>
    {
        public void Configure(EntityTypeBuilder<Plan> builder)
        {
            builder.ToTable("Plans");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                   .IsRequired()
                   .HasMaxLength(100);

            builder.HasIndex(x => x.Name)
                   .IsUnique();

            builder.Property(x => x.Description)
                   .HasMaxLength(500);

            builder.Property(x => x.TrialDays)
                   .IsRequired();

            builder.Property(x => x.CreditLimit)
                   .IsRequired();

            builder.Property(x => x.Price)
                   .HasPrecision(18, 2)
                   .IsRequired();

            builder.Property(x => x.IsActive)
                   .HasDefaultValue(true);
        }
    }
}
