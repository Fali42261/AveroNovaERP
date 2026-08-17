using AveroNova.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AveroNova.Infrastructure.Persistence.Configurations;

public sealed class ClientInstallationConfiguration : IEntityTypeConfiguration<ClientInstallation>
{
    public void Configure(EntityTypeBuilder<ClientInstallation> builder)
    {
        builder.ToTable("ClientInstallations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InstallationId).IsRequired();
        builder.HasIndex(x => x.InstallationId).IsUnique();
        builder.Property(x => x.DeviceId).IsRequired().HasMaxLength(128);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.CompanyId);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
