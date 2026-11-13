using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Vehicles;
using TransBrain.Infrastructure.Persistence.Tenancy;

namespace TransBrain.Infrastructure.Persistence.Configurations;

internal sealed class OrderAdditionalChargeConfiguration : IEntityTypeConfiguration<OrderAdditionalCharge>
{
    public void Configure(EntityTypeBuilder<OrderAdditionalCharge> builder)
    {
        builder.ToTable("order_additional_charges");

        builder.HasKey(c => c.Id);

        builder.HasTenantId();

        builder.Property(c => c.Description)
            .HasMaxLength(OrderAdditionalCharge.MaxDescriptionLength)
            .IsRequired();
        builder.Property(c => c.Quantity).HasPrecision(12, 2).IsRequired();
        builder.Property(c => c.Unit).HasMaxLength(VehicleCatalogEntry.MaxCodeLength);
        builder.Property(c => c.UnitPrice).HasPrecision(12, 2).IsRequired();
        builder.Property(c => c.ModifiedAt).IsRequired();
        builder.Property(c => c.ModifiedBy).HasMaxLength(Vehicle.MaxModifiedByLength);

        // Cascade: deleting an order removes its additional charges with it.
        builder.HasOne<TransportOrder>()
            .WithMany()
            .HasForeignKey(c => c.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.OrderId);
    }
}
