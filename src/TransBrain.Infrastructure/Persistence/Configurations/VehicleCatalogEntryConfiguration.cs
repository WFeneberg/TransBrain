using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Infrastructure.Persistence.Configurations;

internal sealed class VehicleCatalogEntryConfiguration : IEntityTypeConfiguration<VehicleCatalogEntry>
{
    public void Configure(EntityTypeBuilder<VehicleCatalogEntry> builder)
    {
        builder.ToTable("vehicle_catalog_entries");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.Code).HasMaxLength(VehicleCatalogEntry.MaxCodeLength).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(VehicleCatalogEntry.MaxNameLength).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        builder.HasIndex(e => new { e.Kind, e.Code }).IsUnique();

        // The vehicle classes that used to be a fixed list; their codes are the former enum names, so
        // vehicles saved before the catalog existed keep resolving.
        builder.HasData(
            Seed("0199a6c0-0000-7000-8000-000000000001", "Tarpaulin", "Plane"),
            Seed("0199a6c0-0000-7000-8000-000000000002", "Box", "Koffer"),
            Seed("0199a6c0-0000-7000-8000-000000000003", "Refrigerated", "Kühlkoffer"),
            Seed("0199a6c0-0000-7000-8000-000000000004", "Tank", "Tank"),
            Seed("0199a6c0-0000-7000-8000-000000000005", "Silo", "Silo"),
            Seed("0199a6c0-0000-7000-8000-000000000006", "Tipper", "Kipper"),
            Seed("0199a6c0-0000-7000-8000-000000000007", "Flatbed", "Pritsche"),
            Seed("0199a6c0-0000-7000-8000-000000000008", "Container", "Container"),
            Seed("0199a6c0-0000-7000-8000-000000000009", "CarTransporter", "Autotransporter"),
            Seed("0199a6c0-0000-7000-8000-00000000000a", "LowLoader", "Tieflader"),
            Seed("0199a6c0-0000-7000-8000-00000000000b", "Other", "Sonstige"));
    }

    private static object Seed(string id, string code, string name) => new
    {
        Id = Guid.Parse(id),
        Kind = VehicleCatalogKind.VehicleClass,
        Code = code,
        Name = name,
        IsActive = true
    };
}