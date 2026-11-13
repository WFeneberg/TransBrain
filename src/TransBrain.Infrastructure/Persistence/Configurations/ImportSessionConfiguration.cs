using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransBrain.Domain.Imports;
using TransBrain.Infrastructure.Persistence.Imports;
using TransBrain.Infrastructure.Persistence.Tenancy;

namespace TransBrain.Infrastructure.Persistence.Configurations;

internal sealed class ImportSessionConfiguration : IEntityTypeConfiguration<ImportSession>
{
    public void Configure(EntityTypeBuilder<ImportSession> builder)
    {
        builder.ToTable("import_sessions");

        builder.HasKey(s => s.Id);

        builder.HasTenantId();

        builder.Property(s => s.SourceType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(s => s.SourceName).HasMaxLength(ImportSession.MaxSourceNameLength).IsRequired();
        builder.Property(s => s.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(s => s.TargetEntity).HasMaxLength(50);
        builder.Property(s => s.TableName).HasMaxLength(260);

        builder.HasIndex(s => s.CreatedAtUtc);
    }
}

internal sealed class ImportSessionDataConfiguration : IEntityTypeConfiguration<ImportSessionData>
{
    public void Configure(EntityTypeBuilder<ImportSessionData> builder)
    {
        builder.ToTable("import_session_data");

        builder.HasKey(d => d.ImportSessionId);

        builder.HasTenantId();

        builder.Property(d => d.Payload).IsRequired();

        // The payload lives and dies with its session.
        builder.HasOne<ImportSession>()
            .WithOne()
            .HasForeignKey<ImportSessionData>(d => d.ImportSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
