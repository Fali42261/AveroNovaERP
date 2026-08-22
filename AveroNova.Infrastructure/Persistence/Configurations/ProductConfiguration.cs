using AveroNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AveroNova.Infrastructure.Persistence.Configurations
{
    public class ProductConfiguration : IEntityTypeConfiguration<Product>
    {
        public void Configure(EntityTypeBuilder<Product> builder)
        {
            builder.ToTable("Products");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.SKU)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(x => x.Barcode)
                .HasMaxLength(50);

            builder.Property(x => x.Category)
                .HasMaxLength(100);

            builder.Property(x => x.Brand)
                .HasMaxLength(100);

            builder.Property(x => x.Unit)
                .HasMaxLength(20);

            builder.Property(x => x.PurchasePrice)
                .HasPrecision(18, 2);

            builder.Property(x => x.SellingPrice)
                .HasPrecision(18, 2);

            builder.Property(x => x.TaxPercent)
                .HasPrecision(5, 2);

            builder.Property(x => x.DiscountPercent)
                .HasPrecision(5, 2);

            builder.Property(x => x.Description)
                .HasMaxLength(1000);

            builder.Property(x => x.Status)
                .IsRequired();

            builder.Property(x => x.IsDeleted)
                .HasDefaultValue(false);

            builder.HasIndex(x => x.CompanyId);
            builder.HasIndex(x => new { x.CompanyId, x.Name });
            builder.HasIndex(x => new { x.CompanyId, x.SKU });

            builder.HasOne(x => x.Company)
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
