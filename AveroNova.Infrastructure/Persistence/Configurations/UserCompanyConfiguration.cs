using AveroNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AveroNova.Infrastructure.Persistence.Configurations
{
    public class UserCompanyConfiguration : IEntityTypeConfiguration<UserCompany>
    {
        public void Configure(EntityTypeBuilder<UserCompany> builder)
        {
            builder.ToTable("UserCompanies");
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => new { x.UserId, x.CompanyId })
                   .IsUnique();

            builder.HasOne(x => x.User)
                   .WithMany(x => x.UserCompanies)
                   .HasForeignKey(x => x.UserId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Company)
                   .WithMany(x => x.UserCompanies)
                   .HasForeignKey(x => x.CompanyId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
