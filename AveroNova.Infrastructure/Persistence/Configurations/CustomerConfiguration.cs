using AveroNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AveroNova.Infrastructure.Persistence.Configurations
{
    public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
    {
        public void Configure(EntityTypeBuilder<Customer> builder)
        {
            builder.ToTable("Customers");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.MobileNumber)
                .HasMaxLength(15);

            builder.Property(x => x.Email)
                .HasMaxLength(150);

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

            builder.Property(x => x.TaxNumber)
                .HasMaxLength(50);

            builder.Property(x => x.Notes)
                .HasMaxLength(1000);

            builder.Property(x => x.Status)
                .IsRequired();

            builder.Property(x => x.IsDeleted)
                .HasDefaultValue(false);

            builder.HasIndex(x => x.CompanyId);
            builder.HasIndex(x => new { x.CompanyId, x.Name });

            builder.HasOne(x => x.Company)
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
