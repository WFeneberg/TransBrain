using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransBrain.Domain.Customers;
using TransBrain.Domain.Vehicles;
using TransBrain.Infrastructure.Persistence.Tenancy;

namespace TransBrain.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");

        builder.HasKey(c => c.Id);

        builder.HasTenantId();

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

        // No HasDefaultValue on the bools and counts: EF would treat false/0 as "not set" and skip it when
        // the owned value is replaced. The migration still back-fills existing rows.
        builder.OwnsOne(c => c.Additional, additional =>
        {
            additional.Property(a => a.SecondaryName)
                .HasColumnName("secondary_name").HasMaxLength(CustomerAdditionalInfo.MaxNameLength);
            additional.Property(a => a.SecondaryStreet)
                .HasColumnName("secondary_street").HasMaxLength(CustomerAdditionalInfo.MaxStreetLength);
            additional.Property(a => a.SecondaryPostalCode)
                .HasColumnName("secondary_postal_code").HasMaxLength(CustomerAdditionalInfo.MaxPostalCodeLength);
            additional.Property(a => a.SecondaryCity)
                .HasColumnName("secondary_city").HasMaxLength(CustomerAdditionalInfo.MaxCityLength);
            additional.Property(a => a.SecondaryCountry)
                .HasColumnName("secondary_country").HasMaxLength(CustomerAdditionalInfo.CountryLength);
            additional.Property(a => a.FoundedOn).HasColumnName("FoundedOn");
            additional.Property(a => a.WorkerCount).HasColumnName("WorkerCount").IsRequired();
            additional.Property(a => a.SalariedEmployeeCount).HasColumnName("SalariedEmployeeCount").IsRequired();
            additional.Property(a => a.RequiresPalletCheck).HasColumnName("RequiresPalletCheck").IsRequired();
            additional.Property(a => a.ExcludeTollFromInvoice).HasColumnName("ExcludeTollFromInvoice").IsRequired();
            additional.Property(a => a.ReimburseToll).HasColumnName("ReimburseToll").IsRequired();
            additional.Property(a => a.CustomerClass)
                .HasColumnName("CustomerClass").HasMaxLength(VehicleCatalogEntry.MaxCodeLength);
            additional.Property(a => a.Comment)
                .HasColumnName("Comment").HasMaxLength(CustomerAdditionalInfo.MaxCommentLength);
        });

        builder.Navigation(c => c.Additional).IsRequired();
    }
}
