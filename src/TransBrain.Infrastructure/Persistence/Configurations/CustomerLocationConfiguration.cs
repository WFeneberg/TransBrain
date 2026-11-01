using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransBrain.Domain.Customers;

namespace TransBrain.Infrastructure.Persistence.Configurations;

internal sealed class CustomerLocationConfiguration : IEntityTypeConfiguration<CustomerLocation>
{
    public void Configure(EntityTypeBuilder<CustomerLocation> builder)
    {
        builder.ToTable("customer_locations");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Name).HasMaxLength(200).IsRequired();
        builder.Property(l => l.ContactPerson).HasMaxLength(200);
        builder.Property(l => l.Email).HasMaxLength(320);
        builder.Property(l => l.Phone).HasMaxLength(50);

        // Cascade: deleting a customer removes its delivery locations with it.
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(l => l.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => l.CustomerId);

        builder.OwnsOne(l => l.Address, address =>
        {
            address.Property(a => a.Name).HasColumnName("address_name").HasMaxLength(200).IsRequired();
            address.Property(a => a.Street).HasColumnName("address_street").HasMaxLength(200).IsRequired();
            address.Property(a => a.PostalCode).HasColumnName("address_postal_code").HasMaxLength(20).IsRequired();
            address.Property(a => a.City).HasColumnName("address_city").HasMaxLength(200).IsRequired();
            address.Property(a => a.Country).HasColumnName("address_country").HasMaxLength(2).IsRequired();
        });
    }
}
