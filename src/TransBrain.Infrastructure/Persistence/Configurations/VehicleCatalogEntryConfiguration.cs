using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransBrain.Domain.Vehicles;
using TransBrain.Infrastructure.Persistence.Tenancy;

namespace TransBrain.Infrastructure.Persistence.Configurations;

internal sealed class VehicleCatalogEntryConfiguration : IEntityTypeConfiguration<VehicleCatalogEntry>
{
    public void Configure(EntityTypeBuilder<VehicleCatalogEntry> builder)
    {
        builder.ToTable("vehicle_catalog_entries");

        builder.HasKey(e => e.Id);

        builder.HasTenantId();

        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.Code).HasMaxLength(VehicleCatalogEntry.MaxCodeLength).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(VehicleCatalogEntry.MaxNameLength).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();

        // Per tenant: the catalog is tenant-managed master data (carrier groups in particular
        // name tenant-specific companies), so codes only need to be unique within one tenant.
        builder.HasIndex(TenantEntity.PropertyName, nameof(VehicleCatalogEntry.Kind), nameof(VehicleCatalogEntry.Code))
            .IsUnique();

        // The common special document types are seeded as a starting point and can be edited like any entry.
        // The vehicle classes are those of the legacy vehicle screen; their codes are the former enum
        // names where one existed, so vehicles saved before the catalog existed keep resolving. The
        // carrier groups are those of the legacy vehicle screen too, and the phone types and driver
        // document types are the fixed dropdown values of the legacy driver screen ("Zusatz → Telefone"
        // and "Zusatz → Sonderpapiere"). Seeded for the default tenant
        // only: a newly onboarded tenant starts with an empty catalog and fills it via the admin UI.
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
            Seed(VehicleCatalogKind.CustomerClass, "0199a6c0-0000-7000-8000-000000000301", "HuterVilsAll", "Huter Vils alle Gattungen"),
            Seed(VehicleCatalogKind.PhoneType, "0199a6c0-0000-7000-8000-000000000401", "Fax", "Fax"),
            Seed(VehicleCatalogKind.PhoneType, "0199a6c0-0000-7000-8000-000000000402", "Phone", "Telefon"),
            Seed(VehicleCatalogKind.PhoneType, "0199a6c0-0000-7000-8000-000000000403", "Email", "Email"),
            Seed(VehicleCatalogKind.PhoneType, "0199a6c0-0000-7000-8000-000000000404", "CarPhone", "Auto-Telefon"),
            Seed(VehicleCatalogKind.PhoneType, "0199a6c0-0000-7000-8000-000000000405", "HomePage", "Home-Page"),
            Seed(VehicleCatalogKind.PhoneType, "0199a6c0-0000-7000-8000-000000000406", "Private", "Privat"),
            Seed(VehicleCatalogKind.PhoneType, "0199a6c0-0000-7000-8000-000000000407", "Emergency", "Notruf"),
            Seed(VehicleCatalogKind.PhoneType, "0199a6c0-0000-7000-8000-000000000408", "CompanyExtension", "Firma (DW)"),
            Seed(VehicleCatalogKind.DriverDocumentType, "0199a6c0-0000-7000-8000-000000000501", "DangerousGoods", "Gefahrengutschein"),
            Seed(VehicleCatalogKind.DriverDocumentType, "0199a6c0-0000-7000-8000-000000000502", "MedicalOfficer", "Amtsarzt"),
            Seed(VehicleCatalogKind.DriverDocumentType, "0199a6c0-0000-7000-8000-000000000503", "TuvAssessment", "TÜV - Begutachtung"),
            Seed(VehicleCatalogKind.DriverDocumentType, "0199a6c0-0000-7000-8000-000000000504", "B3Certificate", "B-3 - Bescheinigung"),
            Seed(VehicleCatalogKind.DriverDocumentType, "0199a6c0-0000-7000-8000-000000000505", "SpecialApproval", "besondere Zulassung"),
            Seed(VehicleCatalogKind.DriverDocumentType, "0199a6c0-0000-7000-8000-000000000506", "SpecialPermit", "besondere Genehmigung"),
            Seed(VehicleCatalogKind.DriverDocumentType, "0199a6c0-0000-7000-8000-000000000507", "BoilerPressure", "Kesseldruckbescheinigung"),
            Seed(VehicleCatalogKind.DriverDocumentType, "0199a6c0-0000-7000-8000-000000000508", "ServiceInterval", "Service - Intervall"));
    }

    private static object Seed(VehicleCatalogKind kind, string id, string code, string name) => new
    {
        Id = Guid.Parse(id),
        TenantId = Application.Abstractions.TenantDefaults.DefaultTenantId,
        Kind = kind,
        Code = code,
        Name = name,
        IsActive = true
    };
}