using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransBrain.Domain.Billing;
using TransBrain.Infrastructure.Persistence.Tenancy;

namespace TransBrain.Infrastructure.Persistence.Configurations;

internal sealed class InvoiceDesignConfiguration : IEntityTypeConfiguration<InvoiceDesign>
{
    public void Configure(EntityTypeBuilder<InvoiceDesign> builder)
    {
        builder.ToTable("invoice_designs");

        builder.HasKey(d => d.Id);

        builder.HasTenantId();

        // One design per tenant: the designer edits "the" letterhead, not a list of them.
        // Same property as HasTenantId's plain index, so this upgrades it to unique.
        builder.HasIndex(TenantEntity.PropertyName).IsUnique();

        builder.Property(d => d.CompanyName).HasColumnName("company_name").HasMaxLength(200).IsRequired();
        builder.Property(d => d.Street).HasColumnName("street").HasMaxLength(200).IsRequired();
        builder.Property(d => d.PostalCode).HasColumnName("postal_code").HasMaxLength(20).IsRequired();
        builder.Property(d => d.City).HasColumnName("city").HasMaxLength(200).IsRequired();
        builder.Property(d => d.CountryCode).HasColumnName("country_code").HasMaxLength(2).IsRequired();
        builder.Property(d => d.VatId).HasColumnName("vat_id").HasMaxLength(20).IsRequired();
        builder.Property(d => d.TaxNumber).HasColumnName("tax_number").HasMaxLength(30);
        builder.Property(d => d.Email).HasColumnName("email").HasMaxLength(200).IsRequired();
        builder.Property(d => d.Phone).HasColumnName("phone").HasMaxLength(50);
        builder.Property(d => d.Iban).HasColumnName("iban").HasMaxLength(34).IsRequired();
        builder.Property(d => d.Bic).HasColumnName("bic").HasMaxLength(11);
        builder.Property(d => d.BankName).HasColumnName("bank_name").HasMaxLength(100);

        builder.Property(d => d.AccentColor).HasColumnName("accent_color").HasMaxLength(7).IsRequired();
        builder.Property(d => d.FontSizePoints).HasColumnName("font_size_points").IsRequired();
        builder.Property(d => d.LogoPosition)
            .HasColumnName("logo_position")
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();
        builder.Property(d => d.Logo).HasColumnName("logo");
        builder.Property(d => d.LogoContentType).HasColumnName("logo_content_type").HasMaxLength(30);
        builder.Property(d => d.HeaderText).HasColumnName("header_text").HasMaxLength(1000);
        builder.Property(d => d.FooterText).HasColumnName("footer_text").HasMaxLength(1000);
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at").IsRequired();
    }
}
