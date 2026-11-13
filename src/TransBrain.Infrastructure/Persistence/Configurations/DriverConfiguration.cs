using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransBrain.Domain.Drivers;
using TransBrain.Infrastructure.Persistence.Tenancy;

namespace TransBrain.Infrastructure.Persistence.Configurations;

internal sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("drivers");

        builder.HasKey(d => d.Id);

        builder.HasTenantId();

        // Ordered ordinally so this repository and InMemoryDriverRepository, which uses
        // StringComparer.Ordinal, cannot disagree about what "sorted by name" means.
        builder.Property(d => d.LastName).HasMaxLength(100).UseCollation("C").IsRequired();
        builder.Property(d => d.FirstName).HasMaxLength(100).UseCollation("C").IsRequired();

        builder.Property(d => d.LicenseValidUntil).IsRequired();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.ExternalUserId).HasMaxLength(200);

        // Filtered so multiple NULLs remain allowed - most drivers have no Keycloak login yet.
        // Phase 3 will use this column to bind a login (Keycloak's "sub" claim) to exactly one
        // driver, so two drivers silently sharing the same ExternalUserId must be impossible at
        // the database level, not merely discouraged by application code. Scoped per tenant:
        // globally unique would surface a cross-tenant duplicate as an unhandleable 500, because
        // the tenant-filtered duplicate check can never see the other tenant's row.
        builder.HasIndex(TenantEntity.PropertyName, nameof(Driver.ExternalUserId))
            .IsUnique()
            .HasFilter("\"ExternalUserId\" IS NOT NULL");

        builder.HasIndex(d => new { d.LastName, d.FirstName });

        // Same table-sharing setup as CustomerConfiguration's Additional: every column is
        // nullable, and the required navigation makes EF materialize an (all-null) instance
        // for rows written before this data existed.
        builder.OwnsOne(d => d.SocialInsurance, social =>
        {
            social.Property(s => s.DateOfBirth).HasColumnName("DateOfBirth");
            social.Property(s => s.PlaceOfBirth)
                .HasColumnName("PlaceOfBirth").HasMaxLength(DriverSocialInsuranceInfo.MaxPlaceOfBirthLength);
            social.Property(s => s.Nationality)
                .HasColumnName("Nationality").HasMaxLength(DriverSocialInsuranceInfo.MaxNationalityLength);
            social.Property(s => s.SocialInsuranceNumber)
                .HasColumnName("SocialInsuranceNumber")
                .HasMaxLength(DriverSocialInsuranceInfo.MaxSocialInsuranceNumberLength);
            social.Property(s => s.EntryDate).HasColumnName("EntryDate");
            social.Property(s => s.HealthInsurance)
                .HasColumnName("HealthInsurance").HasMaxLength(DriverSocialInsuranceInfo.MaxHealthInsuranceLength);
            social.Property(s => s.BankName)
                .HasColumnName("BankName").HasMaxLength(DriverSocialInsuranceInfo.MaxBankNameLength);
            social.Property(s => s.BankCode)
                .HasColumnName("BankCode").HasMaxLength(DriverSocialInsuranceInfo.MaxBankCodeLength);
            social.Property(s => s.AccountNumber)
                .HasColumnName("AccountNumber").HasMaxLength(DriverSocialInsuranceInfo.MaxAccountNumberLength);
            social.Property(s => s.Bic)
                .HasColumnName("Bic").HasMaxLength(DriverSocialInsuranceInfo.MaxBicLength);
            social.Property(s => s.Iban)
                .HasColumnName("Iban").HasMaxLength(DriverSocialInsuranceInfo.MaxIbanLength);
            social.Property(s => s.FamilyDoctor)
                .HasColumnName("FamilyDoctor").HasMaxLength(DriverSocialInsuranceInfo.MaxFamilyDoctorLength);
            social.Property(s => s.FamilyDoctorPhone)
                .HasColumnName("FamilyDoctorPhone")
                .HasMaxLength(DriverSocialInsuranceInfo.MaxFamilyDoctorPhoneLength);
        });

        builder.Navigation(d => d.SocialInsurance).IsRequired();

        // Same table-sharing setup as SocialInsurance above.
        builder.OwnsOne(d => d.Documents, documents =>
        {
            documents.Property(x => x.LicenseNumber)
                .HasColumnName("LicenseNumber").HasMaxLength(DriverDocumentsInfo.MaxLicenseNumberLength);
            documents.Property(x => x.IssuingAuthority)
                .HasColumnName("IssuingAuthority").HasMaxLength(DriverDocumentsInfo.MaxIssuingAuthorityLength);
            documents.Property(x => x.IssueDate).HasColumnName("LicenseIssueDate");
        });

        builder.Navigation(d => d.Documents).IsRequired();

        builder.OwnsOne(d => d.ClothingSizes, sizes =>
        {
            sizes.Property(s => s.ClothingSize)
                .HasColumnName("ClothingSize").HasMaxLength(DriverClothingSizes.MaxSizeLength);
            sizes.Property(s => s.ShoeSize)
                .HasColumnName("ShoeSize").HasMaxLength(DriverClothingSizes.MaxSizeLength);
            sizes.Property(s => s.GloveSize)
                .HasColumnName("GloveSize").HasMaxLength(DriverClothingSizes.MaxSizeLength);
        });

        builder.Navigation(d => d.ClothingSizes).IsRequired();

        // Same child-table setup as CustomerLocationConfiguration's Contacts: EF's shadow key owns
        // identity, the sequence is just the order and is renumbered on every replace.
        builder.OwnsMany(d => d.Relatives, relative =>
        {
            relative.ToTable("driver_relatives");
            relative.WithOwner().HasForeignKey("driver_id");
            relative.Property(r => r.Sequence).HasColumnName("sequence").ValueGeneratedNever().IsRequired();
            relative.Property(r => r.LastName).HasColumnName("last_name")
                .HasMaxLength(DriverRelative.MaxNameLength).IsRequired();
            relative.Property(r => r.FirstName).HasColumnName("first_name")
                .HasMaxLength(DriverRelative.MaxNameLength);
            relative.Property(r => r.Relationship).HasColumnName("relationship")
                .HasMaxLength(DriverRelative.MaxRelationshipLength);
            relative.Property(r => r.DateOfBirth).HasColumnName("date_of_birth");
            relative.Property(r => r.Phone).HasColumnName("phone")
                .HasMaxLength(DriverRelative.MaxPhoneLength);
        });

        builder.Navigation(d => d.Relatives).HasField("_relatives")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(d => d.SpecialDocuments, document =>
        {
            document.ToTable("driver_special_documents");
            document.WithOwner().HasForeignKey("driver_id");
            document.Property(x => x.Sequence).HasColumnName("sequence").ValueGeneratedNever().IsRequired();
            document.Property(x => x.Type).HasColumnName("type")
                .HasMaxLength(DriverSpecialDocument.MaxTypeLength).IsRequired();
            document.Property(x => x.LastInspectionDate).HasColumnName("last_inspection_date");
            document.Property(x => x.ValidUntil).HasColumnName("valid_until");
            document.Property(x => x.Comment).HasColumnName("comment")
                .HasMaxLength(DriverSpecialDocument.MaxCommentLength);
        });

        builder.Navigation(d => d.SpecialDocuments).HasField("_specialDocuments")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsMany(d => d.PhoneNumbers, phone =>
        {
            phone.ToTable("driver_phone_numbers");
            phone.WithOwner().HasForeignKey("driver_id");
            phone.Property(p => p.Sequence).HasColumnName("sequence").ValueGeneratedNever().IsRequired();
            phone.Property(p => p.Number).HasColumnName("number")
                .HasMaxLength(DriverPhoneNumber.MaxNumberLength).IsRequired();
            phone.Property(p => p.Type).HasColumnName("type")
                .HasMaxLength(DriverPhoneNumber.MaxTypeLength);
            phone.Property(p => p.Comment).HasColumnName("comment")
                .HasMaxLength(DriverPhoneNumber.MaxCommentLength);
        });

        builder.Navigation(d => d.PhoneNumbers).HasField("_phoneNumbers")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property<string>("LicenseClassesRaw")
            .HasColumnName("license_classes")
            .HasMaxLength(50)
            .IsRequired();

        builder.Ignore(d => d.LicenseClasses);
    }
}
