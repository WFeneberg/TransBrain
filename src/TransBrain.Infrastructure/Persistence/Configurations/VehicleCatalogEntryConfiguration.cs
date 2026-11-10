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

        // The common special document types are seeded as a starting point and can be edited like any entry.
        // The vehicle classes are those of the legacy vehicle screen; their codes are the former enum
        // names where one existed, so vehicles saved before the catalog existed keep resolving. The
        // carrier groups are those of the legacy vehicle screen too.
        builder.HasData(
            Seed(VehicleCatalogKind.VehicleClass, "0199a6c0-0000-7000-8000-00000000000b", "Other", "Sonstige"),
            Seed(VehicleCatalogKind.VehicleClass, "0199a6c0-0000-7000-8000-000000000004", "Tank", "Tank"),
            Seed(VehicleCatalogKind.VehicleClass, "0199a6c0-0000-7000-8000-000000000003", "Refrigerated", "Kühler"),
            Seed(VehicleCatalogKind.VehicleClass, "0199a6c0-0000-7000-8000-000000000001", "Tarpaulin", "Plane"),
            Seed(VehicleCatalogKind.VehicleClass, "0199a6c0-0000-7000-8000-000000000006", "Tipper", "Kipper"),
            Seed(VehicleCatalogKind.VehicleClass, "0199a6c0-0000-7000-8000-00000000000c", "Rental", "Leihfahrzeug"),
                        Seed(VehicleCatalogKind.CarrierGroup, "0199a6c0-0000-7000-8000-000000000201", "Other", "Sonstige"),
                        Seed(VehicleCatalogKind.CarrierGroup, "0199a6c0-0000-7000-8000-000000000202", "HuterVils", "Huter Vils"),
                        Seed(VehicleCatalogKind.CarrierGroup, "0199a6c0-0000-7000-8000-000000000203", "HuterFuessen", "Huter Füssen"),
                        Seed(VehicleCatalogKind.CarrierGroup, "0199a6c0-0000-7000-8000-000000000204", "Fixed", "Fixe Frächter"),
                        Seed(VehicleCatalogKind.CarrierGroup, "0199a6c0-0000-7000-8000-000000000205", "Regular", "Regelmäßig beschäftigte Frächter"),
                        Seed(VehicleCatalogKind.CarrierGroup, "0199a6c0-0000-7000-8000-000000000206", "Rental", "Leihfahrzeuge"),
                        Seed(VehicleCatalogKind.DocumentType, "0199a6c0-0000-7000-8000-000000000101", "ADR", "ADR-Zulassungsbescheinigung"),
            Seed(VehicleCatalogKind.DocumentType, "0199a6c0-0000-7000-8000-000000000102", "ATP", "ATP-Bescheinigung"),
            Seed(VehicleCatalogKind.DocumentType, "0199a6c0-0000-7000-8000-000000000103", "CEMT", "CEMT-Genehmigung"),
            Seed(VehicleCatalogKind.DocumentType, "0199a6c0-0000-7000-8000-000000000104", "Tachograph", "Tachographenprüfung"),
            Seed(VehicleCatalogKind.DocumentType, "0199a6c0-0000-7000-8000-000000000105", "UVV", "UVV-Prüfung"),
            Seed(VehicleCatalogKind.DocumentType, "0199a6c0-0000-7000-8000-000000000106", "Crane", "Kranprüfung"),
            Seed(VehicleCatalogKind.DocumentType, "0199a6c0-0000-7000-8000-000000000107", "TailLift", "Ladebordwandprüfung"),
            Seed(VehicleCatalogKind.CustomerClass, "0199a6c0-0000-7000-8000-000000000301", "HuterVilsAll", "Huter Vils alle Gattungen"));
    }

    private static object Seed(VehicleCatalogKind kind, string id, string code, string name) => new
    {
        Id = Guid.Parse(id),
        Kind = kind,
        Code = code,
        Name = name,
        IsActive = true
    };
}