using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Infrastructure.Persistence.Configurations;

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");

        builder.HasKey(v => v.Id);

        // The converter round-trips through the domain factory; .Value is safe here because only
        // already-validated plates ever reach the database.
        // "C" is PostgreSQL's byte-ordering collation; it is what actually makes the database's
        // ORDER BY agree with StringComparer.Ordinal used by InMemoryVehicleRepository.
        builder.Property(v => v.LicensePlate)
            .HasConversion(plate => plate.Value, value => LicensePlate.Create(value).Value)
            .HasMaxLength(15)
            .UseCollation("C")
            .IsRequired();

        builder.HasIndex(v => v.LicensePlate).IsUnique();

        builder.Property(v => v.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(v => v.PayloadKg).IsRequired();
        builder.Property(v => v.LoadMeters).HasPrecision(6, 2).IsRequired();
        builder.Property(v => v.NextInspectionDue).IsRequired();
        builder.Property(v => v.ModifiedBy).HasMaxLength(Vehicle.MaxModifiedByLength);

        // Deleting a driver must not delete or block their vehicles; it only clears the assignment.
        builder.HasOne<Driver>()
            .WithMany()
            .HasForeignKey(v => v.DefaultDriverId)
            .OnDelete(DeleteBehavior.SetNull);

        // Unprefixed column names: the details are plain vehicle attributes, the owned type only
        // groups them in the domain.
        builder.OwnsOne(v => v.Details, details =>
        {
            details.Property(d => d.IsLicensePlateConfidential)
                .HasColumnName("IsLicensePlateConfidential").IsRequired();
            details.Property(d => d.Make).HasColumnName("Make").HasMaxLength(VehicleDetails.MaxNameLength);
            details.Property(d => d.Model).HasColumnName("Model").HasMaxLength(VehicleDetails.MaxNameLength);
            details.Property(d => d.ManufacturedOn).HasColumnName("ManufacturedOn");
            details.Property(d => d.PowerKw).HasColumnName("PowerKw");
            details.Property(d => d.VehicleClass)
                .HasColumnName("VehicleClass").HasMaxLength(VehicleCatalogEntry.MaxCodeLength);
            details.Property(d => d.CarrierGroup)
                .HasColumnName("CarrierGroup").HasMaxLength(VehicleDetails.MaxNameLength);
            details.Property(d => d.ChassisNumber)
                .HasColumnName("ChassisNumber").HasMaxLength(VehicleDetails.MaxIdentifierLength);
            details.Property(d => d.EngineNumber)
                .HasColumnName("EngineNumber").HasMaxLength(VehicleDetails.MaxIdentifierLength);
            details.Property(d => d.LengthMm).HasColumnName("LengthMm");
            details.Property(d => d.WidthMm).HasColumnName("WidthMm");
            details.Property(d => d.HeightMm).HasColumnName("HeightMm");
            details.Property(d => d.CurbWeightKg).HasColumnName("CurbWeightKg");
            details.Property(d => d.GrossWeightKg).HasColumnName("GrossWeightKg");
            details.Property(d => d.MaxFifthWheelLoadKg).HasColumnName("MaxFifthWheelLoadKg");
            details.Property(d => d.Notes).HasColumnName("Notes").HasMaxLength(VehicleDetails.MaxNotesLength);
            details.Property(d => d.DataSheet)
                .HasColumnName("DataSheet").HasMaxLength(VehicleDetails.MaxDataSheetLength);
        });

        builder.Navigation(v => v.Details).IsRequired();

        // No HasDefaultValue on the bools: EF would treat false as "not set" and skip it when the owned
        // value is replaced, so unticking a box would not persist. The migration still back-fills false.
        builder.OwnsOne(v => v.Equipment, equipment =>
        {
            equipment.Property(e => e.HasPartitionWall)
                .HasColumnName("HasPartitionWall").IsRequired();
            equipment.Property(e => e.HasPalletBasket)
                .HasColumnName("HasPalletBasket").IsRequired();
            equipment.Property(e => e.HasDangerousGoodsEquipment)
                .HasColumnName("HasDangerousGoodsEquipment").IsRequired();
            equipment.Property(e => e.IsDangerousGoodsApproved)
                .HasColumnName("IsDangerousGoodsApproved").IsRequired();
            equipment.Property(e => e.HasLiftAxle)
                .HasColumnName("HasLiftAxle").IsRequired();
            equipment.Property(e => e.HasDoubleDeck)
                .HasColumnName("HasDoubleDeck").IsRequired();
            equipment.Property(e => e.HasDoubleDeckBeams)
                .HasColumnName("HasDoubleDeckBeams").IsRequired();
            equipment.Property(e => e.HasSmallLadder)
                .HasColumnName("HasSmallLadder").IsRequired();
            equipment.Property(e => e.HasLargeLadder)
                .HasColumnName("HasLargeLadder").IsRequired();
            equipment.Property(e => e.TankVolumeLiters).HasColumnName("TankVolumeLiters");
            equipment.Property(e => e.InteriorLengthMm).HasColumnName("InteriorLengthMm");
            equipment.Property(e => e.InteriorWidthMm).HasColumnName("InteriorWidthMm");
            equipment.Property(e => e.InteriorHeightMm).HasColumnName("InteriorHeightMm");
            equipment.Property(e => e.SupportPoleCount).HasColumnName("SupportPoleCount");
            equipment.Property(e => e.LoadBarCount).HasColumnName("LoadBarCount");
            equipment.Property(e => e.LashingStrapCount).HasColumnName("LashingStrapCount");
            equipment.Property(e => e.PalletCount).HasColumnName("PalletCount");
            equipment.Property(e => e.ChainTensionerCount).HasColumnName("ChainTensionerCount");
            equipment.Property(e => e.MarbleRackCount).HasColumnName("MarbleRackCount");
            equipment.Property(e => e.MarbleRackMaterial)
                .HasColumnName("MarbleRackMaterial").HasConversion<string>().HasMaxLength(20);
        });

        builder.Navigation(v => v.Equipment).IsRequired();

        builder.OwnsOne(v => v.Commercial, commercial =>
        {
            commercial.Property(c => c.PurchasePrice).HasColumnName("PurchasePrice").HasPrecision(10, 2);
            commercial.Property(c => c.PurchasedOn).HasColumnName("PurchasedOn");
            commercial.Property(c => c.PurchaseOdometerKm).HasColumnName("PurchaseOdometerKm");
            commercial.Property(c => c.FirstRegisteredOn).HasColumnName("FirstRegisteredOn");
            commercial.Property(c => c.UsefulLifeYears).HasColumnName("UsefulLifeYears");
            commercial.Property(c => c.UsefulLifeKm).HasColumnName("UsefulLifeKm");
            commercial.Property(c => c.CostCenter)
                .HasColumnName("CostCenter").HasMaxLength(VehicleCommercialInfo.MaxCostCenterLength);
            commercial.Property(c => c.VehicleInsurer)
                .HasColumnName("VehicleInsurer").HasMaxLength(VehicleCatalogEntry.MaxCodeLength);
            commercial.Property(c => c.VehicleInsurancePolicyNumbers)
                .HasColumnName("VehicleInsurancePolicyNumbers").HasMaxLength(VehicleCommercialInfo.MaxPolicyNumbersLength);
            commercial.Property(c => c.TransportInsurer)
                .HasColumnName("TransportInsurer").HasMaxLength(VehicleCatalogEntry.MaxCodeLength);
            commercial.Property(c => c.TransportInsurancePolicyNumbers)
                .HasColumnName("TransportInsurancePolicyNumbers").HasMaxLength(VehicleCommercialInfo.MaxPolicyNumbersLength);
        });

        builder.Navigation(v => v.Commercial).IsRequired();
    }
}

