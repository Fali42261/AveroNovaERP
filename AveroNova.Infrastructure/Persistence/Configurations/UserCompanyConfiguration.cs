using AveroNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AveroNova.Infrastructure.Persistence.Configurations;

public class UserCompanyConfiguration : IEntityTypeConfiguration<UserCompany>
{
    public void Configure(EntityTypeBuilder<UserCompany> builder)
    {
        builder.ToTable("UserCompanies");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserId, x.CompanyId }).IsUnique();
        builder.Property(x => x.SyncStatus).HasConversion<int>();
        builder.Property(x => x.SyncVersion).HasDefaultValue(1L);
    }
}
