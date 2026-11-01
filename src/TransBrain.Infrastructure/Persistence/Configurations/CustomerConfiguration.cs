using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransBrain.Domain.Customers;

namespace TransBrain.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(c => c.Id);

        // Ordered ordinally so this repository and InMemoryCustomerRepository cannot disagree
        // about what "sorted by name" means.
        builder.Property(c => c.Name).HasMaxLength(200).UseCollation("C").IsRequired();
        builder.HasIndex(c => c.Name);

        builder.Property(c => c.ContactPerson).HasMaxLength(200);
        builder.Property(c => c.Email).HasMaxLength(320);
        builder.Property(c => c.Phone).HasMaxLength(50);
        builder.Property(c => c.VatId).HasMaxLength(20);
        builder.Property(c => c.PaymentTermDays).IsRequired();

        builder.OwnsOne(c => c.Address, address =>
        {
            address.Property(a => a.Name).HasColumnName("address_name").HasMaxLength(200).IsRequired();
            address.Property(a => a.Street).HasColumnName("address_street").HasMaxLength(200).IsRequired();
            address.Property(a => a.PostalCode).HasColumnName("address_postal_code").HasMaxLength(20).IsRequired();
            address.Property(a => a.City).HasColumnName("address_city").HasMaxLength(200).IsRequired();
            address.Property(a => a.Country).HasColumnName("address_country").HasMaxLength(2).IsRequired();
        });
    }
}
