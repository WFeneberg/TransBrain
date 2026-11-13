using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Vehicles;
using TransBrain.Infrastructure.Persistence.Tenancy;

namespace TransBrain.Infrastructure.Persistence.Configurations;

internal sealed class OrderTollChargeConfiguration : IEntityTypeConfiguration<OrderTollCharge>
{
    public void Configure(EntityTypeBuilder<OrderTollCharge> builder)
    {
        builder.ToTable("order_toll_charges");

        builder.HasKey(t => t.Id);

        builder.HasTenantId();

        builder.Property(t => t.TollCategory).HasMaxLength(VehicleCatalogEntry.MaxCodeLength).IsRequired();
        builder.Property(t => t.EmptyKm).IsRequired();
        builder.Property(t => t.LoadedKm).IsRequired();
        builder.Property(t => t.FactorPerKm).HasPrecision(8, 4).IsRequired();
        builder.Property(t => t.ModifiedAt).IsRequired();
        builder.Property(t => t.ModifiedBy).HasMaxLength(Vehicle.MaxModifiedByLength);

        // Cascade: deleting an order removes its toll records with it.
        builder.HasOne<TransportOrder>()
            .WithMany()
            .HasForeignKey(t => t.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.OrderId);
    }
}
