using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransBrain.Domain.Routing;

namespace TransBrain.Infrastructure.Persistence.Configurations;

internal sealed class GeoOverrideConfiguration : IEntityTypeConfiguration<GeoOverride>
{
    public void Configure(EntityTypeBuilder<GeoOverride> builder)
    {
        builder.ToTable("geo_overrides");

        builder.HasKey(o => o.Id);

        // Lengths match the address columns elsewhere; the key is their joined lower-case form.
        builder.Property(o => o.Street).HasColumnName("street").HasMaxLength(200).IsRequired();
        builder.Property(o => o.PostalCode).HasColumnName("postal_code").HasMaxLength(20).IsRequired();
        builder.Property(o => o.City).HasColumnName("city").HasMaxLength(200).IsRequired();
        builder.Property(o => o.Country).HasColumnName("country").HasMaxLength(2).IsRequired();
        builder.Property(o => o.AddressKey).HasColumnName("address_key").HasMaxLength(450).IsRequired();
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(o => o.AddressKey).IsUnique();

        builder.OwnsOne(o => o.Position, position =>
        {
            position.Property(p => p.Latitude).HasColumnName("latitude").IsRequired();
            position.Property(p => p.Longitude).HasColumnName("longitude").IsRequired();
        });

        builder.Navigation(o => o.Position).IsRequired();
    }
}
