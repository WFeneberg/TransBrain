using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransBrain.Domain.Drivers;

namespace TransBrain.Infrastructure.Persistence.Configurations;

internal sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("drivers");

        builder.HasKey(d => d.Id);

        // Ordered ordinally so this repository and InMemoryDriverRepository, which uses
        // StringComparer.Ordinal, cannot disagree about what "sorted by name" means.
        builder.Property(d => d.LastName).HasMaxLength(100).UseCollation("C").IsRequired();
        builder.Property(d => d.FirstName).HasMaxLength(100).UseCollation("C").IsRequired();

        builder.Property(d => d.LicenseValidUntil).IsRequired();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.ExternalUserId).HasMaxLength(200);

        // Filtered so multiple NULLs remain allowed - most drivers have no Keycloak login yet.
        // Phase 3 will use this column to bind a login (Keycloak's "sub" claim) to exactly one
        // driver, so two drivers silently sharing the same ExternalUserId must be impossible at
        // the database level, not merely discouraged by application code.
        builder.HasIndex(d => d.ExternalUserId)
            .IsUnique()
            .HasFilter("\"ExternalUserId\" IS NOT NULL");

        builder.HasIndex(d => new { d.LastName, d.FirstName });

        builder.Property<string>("LicenseClassesRaw")
            .HasColumnName("license_classes")
            .HasMaxLength(50)
            .IsRequired();

        builder.Ignore(d => d.LicenseClasses);
    }
}
