using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransBrain.Domain.Orders;

namespace TransBrain.Infrastructure.Persistence.Configurations;

internal sealed class TransportOrderConfiguration : IEntityTypeConfiguration<TransportOrder>
{
    public void Configure(EntityTypeBuilder<TransportOrder> builder)
    {
        builder.ToTable("transport_orders");

        builder.HasKey(o => o.Id);

        // Ordered ordinally so this repository and InMemoryTransportOrderRepository, which uses
        // StringComparer.Ordinal, cannot disagree about what "sorted by order number" means.
        builder.Property(o => o.OrderNumber)
            .HasConversion(number => number.Value, value => OrderNumber.Parse(value).Value)
            .HasColumnName("order_number")
            .HasMaxLength(20)
            .UseCollation("C")
            .IsRequired();

        builder.HasIndex(o => o.OrderNumber).IsUnique();

        builder.OwnsOne(o => o.Consignor, ConfigureAddress("consignor"));
        builder.OwnsOne(o => o.Consignee, ConfigureAddress("consignee"));

        builder.OwnsOne(o => o.Cargo, cargo =>
        {
            cargo.Property(c => c.Description).HasColumnName("cargo_description").HasMaxLength(500).IsRequired();
            cargo.Property(c => c.WeightKg).HasColumnName("cargo_weight_kg").IsRequired();
            cargo.Property(c => c.LoadMeters).HasColumnName("cargo_load_meters").HasPrecision(6, 2).IsRequired();
        });

        builder.OwnsOne(o => o.PickupWindow, window =>
        {
            window.Property(w => w.From).HasColumnName("pickup_from").IsRequired();
            window.Property(w => w.To).HasColumnName("pickup_to").IsRequired();

            // Declared inside the OwnsOne block, not as builder.HasIndex("pickup_from"): the
            // column belongs to the owned type, so the outer builder would silently create a
            // shadow property of that name on transport_orders and index nothing useful. The
            // list filters on this column, and an unindexed filter gets slow.
            window.HasIndex(w => w.From);
        });

        builder.OwnsOne(o => o.DeliveryWindow, window =>
        {
            window.Property(w => w.From).HasColumnName("delivery_from").IsRequired();
            window.Property(w => w.To).HasColumnName("delivery_to").IsRequired();
        });

        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(o => o.CreatedAt).IsRequired();
    }

    private static Action<OwnedNavigationBuilder<TransportOrder, Domain.Common.Address>> ConfigureAddress(
        string prefix)
        => address =>
        {
            address.Property(a => a.Name).HasColumnName($"{prefix}_name").HasMaxLength(200).IsRequired();
            address.Property(a => a.Street).HasColumnName($"{prefix}_street").HasMaxLength(200).IsRequired();
            address.Property(a => a.PostalCode).HasColumnName($"{prefix}_postal_code").HasMaxLength(20).IsRequired();
            address.Property(a => a.City).HasColumnName($"{prefix}_city").HasMaxLength(200).IsRequired();
            address.Property(a => a.Country).HasColumnName($"{prefix}_country").HasMaxLength(2).IsRequired();
        };
}
