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

        builder.OwnsMany(l => l.Contacts, contact =>
        {
            contact.ToTable("customer_location_contacts");
            contact.WithOwner().HasForeignKey("customer_location_id");
            // Not part of the key: replacing the contacts renumbers them, and EF cannot update a
            // primary key in place. EF's shadow key owns identity; the sequence is just the order.
            contact.Property(c => c.Sequence).HasColumnName("sequence").ValueGeneratedNever().IsRequired();
            contact.Property(c => c.Name).HasColumnName("name")
                .HasMaxLength(CustomerLocationContact.MaxNameLength).IsRequired();
            contact.Property(c => c.Phone).HasColumnName("phone").HasMaxLength(CustomerLocationContact.MaxPhoneLength);
            contact.Property(c => c.Email).HasColumnName("email").HasMaxLength(CustomerLocationContact.MaxEmailLength);
        });

        // Through the backing field: the entity exposes its contacts read-only on purpose.
        builder.Navigation(l => l.Contacts).HasField("_contacts").UsePropertyAccessMode(PropertyAccessMode.Field);

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
