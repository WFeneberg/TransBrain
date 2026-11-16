using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransBrain.Domain.Vehicles;
using TransBrain.Infrastructure.Persistence.Tenancy;

namespace TransBrain.Infrastructure.Persistence.Configurations;

internal sealed class VehicleMaintenanceConfiguration : IEntityTypeConfiguration<VehicleMaintenance>
{
    public void Configure(EntityTypeBuilder<VehicleMaintenance> builder)
    {
        builder.ToTable("vehicle_maintenances");

        builder.HasKey(m => m.Id);

        builder.HasTenantId();

        builder.Property(m => m.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.PerformedOn).IsRequired();
        builder.Property(m => m.Description).HasMaxLength(VehicleMaintenance.MaxDescriptionLength).IsRequired();
        builder.Property(m => m.Workshop).HasMaxLength(VehicleMaintenance.MaxWorkshopLength);
        builder.Property(m => m.Cost).HasPrecision(12, 2);
        builder.Property(m => m.ModifiedAt).IsRequired();
        builder.Property(m => m.ModifiedBy).HasMaxLength(Vehicle.MaxModifiedByLength);

        // Cascade: deleting a vehicle removes its maintenance history with it.
        builder.HasOne<Vehicle>()
            .WithMany()
            .HasForeignKey(m => m.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Serves the history listing, which is always per vehicle and ordered by date.
        builder.HasIndex(m => new { m.VehicleId, m.PerformedOn });
    }
}
