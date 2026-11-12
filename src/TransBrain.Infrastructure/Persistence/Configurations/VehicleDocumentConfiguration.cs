using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransBrain.Domain.Vehicles;
using TransBrain.Infrastructure.Persistence.Tenancy;

namespace TransBrain.Infrastructure.Persistence.Configurations;

internal sealed class VehicleDocumentConfiguration : IEntityTypeConfiguration<VehicleDocument>
{
    public void Configure(EntityTypeBuilder<VehicleDocument> builder)
    {
        builder.ToTable("vehicle_documents");

        builder.HasKey(d => d.Id);

        builder.HasTenantId();

        builder.Property(d => d.DocumentType).HasMaxLength(VehicleCatalogEntry.MaxCodeLength).IsRequired();
        builder.Property(d => d.Remark).HasMaxLength(VehicleDocument.MaxRemarkLength);
        builder.Property(d => d.ModifiedAt).IsRequired();
        builder.Property(d => d.ModifiedBy).HasMaxLength(Vehicle.MaxModifiedByLength);

        // Cascade: deleting a vehicle removes its special documents with it.
        builder.HasOne<Vehicle>()
            .WithMany()
            .HasForeignKey(d => d.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Also serves the lookups by vehicle, so no separate VehicleId index is needed.
        builder.HasIndex(d => new { d.VehicleId, d.DocumentType }).IsUnique();
    }
}