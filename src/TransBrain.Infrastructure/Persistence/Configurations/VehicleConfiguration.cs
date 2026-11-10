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
                .HasColumnName("IsLicensePlateConfidential").HasDefaultValue(false).IsRequired();
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
        });

        builder.Navigation(v => v.Details).IsRequired();
    }
}

