using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransBrain.Domain.Tours;

namespace TransBrain.Infrastructure.Persistence.Configurations;

internal sealed class TourConfiguration : IEntityTypeConfiguration<Tour>
{
    public void Configure(EntityTypeBuilder<Tour> builder)
    {
        builder.ToTable("tours");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TourDate).HasColumnName("tour_date").IsRequired();
        builder.Property(t => t.VehicleId).HasColumnName("vehicle_id").IsRequired();
        builder.Property(t => t.DriverId).HasColumnName("driver_id").IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // The two invariants no single object can check. Spec §5.5 allows a vehicle and a
        // driver at most one tour per date, with no exception for a completed one, so these are
        // unconditional. Enforcing them here rather than with a pre-flight query is what makes
        // them hold under concurrency: an index serialises, a SELECT does not.
        builder.HasIndex(t => new { t.TourDate, t.VehicleId })
            .IsUnique()
            .HasDatabaseName("ix_tours_date_vehicle_unique");

        builder.HasIndex(t => new { t.TourDate, t.DriverId })
            .IsUnique()
            .HasDatabaseName("ix_tours_date_driver_unique");

        builder.OwnsMany(t => t.Stops, stop =>
        {
            stop.ToTable("tour_stops");
            stop.WithOwner().HasForeignKey("tour_id");
            // ValueGeneratedNever, and deliberately NOT part of the key. Sequence is domain
            // data the aggregate assigns and RENUMBERS when an order is removed; a renumber
            // would be an in-place primary-key update, which EF cannot do. Left as an ordinary
            // column, EF's own shadow key owns identity and renumbering is just a write.
            stop.Property(s => s.Sequence).HasColumnName("sequence").ValueGeneratedNever().IsRequired();
            stop.Property(s => s.TransportOrderId).HasColumnName("transport_order_id").IsRequired();
            stop.Property(s => s.StopType).HasColumnName("stop_type").HasConversion<string>()
                .HasMaxLength(20).IsRequired();
        });

        // The backing field, not the IReadOnlyList property: the aggregate exposes its stops
        // read-only on purpose, and EF must write through the field to respect that.
        builder.Navigation(t => t.Stops).HasField("_stops").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
