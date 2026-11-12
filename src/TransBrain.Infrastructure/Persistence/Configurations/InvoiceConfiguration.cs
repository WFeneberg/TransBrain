using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Customers;
using TransBrain.Domain.Orders;
using TransBrain.Infrastructure.Persistence.Tenancy;

namespace TransBrain.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");

        builder.HasKey(i => i.Id);

        builder.HasTenantId();

        // Ordered ordinally so this repository and InMemoryInvoiceRepository, which uses
        // StringComparer.Ordinal, cannot disagree about what "sorted by invoice number" means.
        builder.Property(i => i.InvoiceNumber)
            .HasConversion(number => number.Value, value => InvoiceNumber.Parse(value).Value)
            .HasColumnName("invoice_number")
            .HasMaxLength(20)
            .UseCollation("C")
            .IsRequired();

        // Unique per tenant: each tenant draws numbers from its own sequence, so "RE-2026-00001"
        // legitimately exists once per tenant.
        builder.HasIndex(TenantEntity.PropertyName, nameof(Invoice.InvoiceNumber)).IsUnique();

        builder.Property(i => i.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.HasIndex(i => i.CustomerId);

        // Restrict, not SetNull or Cascade: an invoice is a retained business document, and a
        // customer with invoices on file must not be deletable out from under them.
        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(i => i.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(i => i.CustomerName).HasColumnName("customer_name").HasMaxLength(200).IsRequired();
        builder.Property(i => i.CustomerVatId).HasColumnName("customer_vat_id").HasMaxLength(20);
        builder.Property(i => i.PaymentTermDays).HasColumnName("payment_term_days").IsRequired();
        builder.Property(i => i.VatRatePercent)
            .HasColumnName("vat_rate_percent")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.OwnsOne(i => i.BillingAddress, address =>
        {
            address.Property(a => a.Name).HasColumnName("billing_name").HasMaxLength(200).IsRequired();
            address.Property(a => a.Street).HasColumnName("billing_street").HasMaxLength(200).IsRequired();
            address.Property(a => a.PostalCode).HasColumnName("billing_postal_code").HasMaxLength(20).IsRequired();
            address.Property(a => a.City).HasColumnName("billing_city").HasMaxLength(200).IsRequired();
            address.Property(a => a.Country).HasColumnName("billing_country").HasMaxLength(2).IsRequired();
        });

        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(i => i.IssuedAt).HasColumnName("issued_at");
        builder.Property(i => i.DueDate).HasColumnName("due_date");
        builder.Property(i => i.PaidAt).HasColumnName("paid_at");

        builder.OwnsMany(i => i.Lines, line =>
        {
            line.ToTable("invoice_lines");
            line.WithOwner().HasForeignKey("invoice_id");
            // ValueGeneratedNever, and deliberately NOT part of the key, for the same reason as
            // tour_stops.sequence: Position is domain data the aggregate RENUMBERS when a line
            // is removed, and a renumber must not be a primary-key update.
            line.Property(l => l.Position).HasColumnName("position").ValueGeneratedNever().IsRequired();
            line.Property(l => l.TransportOrderId).HasColumnName("transport_order_id").IsRequired();
            line.Property(l => l.OrderNumber).HasColumnName("order_number").HasMaxLength(20).IsRequired();
            line.Property(l => l.Description).HasColumnName("description").HasMaxLength(500).IsRequired();
            line.Property(l => l.NetAmount).HasColumnName("net_amount").HasPrecision(12, 2).IsRequired();

            // The billed-orders lookup filters on this column for every invoice creation and
            // every billable-orders listing; unindexed it degrades with billing history.
            line.HasIndex(l => l.TransportOrderId);

            // Restrict: a billed order is referenced by a business document and must not be
            // deletable while an invoice line points at it.
            line.HasOne<TransportOrder>()
                .WithMany()
                .HasForeignKey(l => l.TransportOrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // The backing field, not the IReadOnlyList property: the aggregate exposes its lines
        // read-only on purpose, and EF must write through the field to respect that.
        builder.Navigation(i => i.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
