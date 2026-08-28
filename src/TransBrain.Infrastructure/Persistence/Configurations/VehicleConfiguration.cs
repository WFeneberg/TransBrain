using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
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
    }
}
