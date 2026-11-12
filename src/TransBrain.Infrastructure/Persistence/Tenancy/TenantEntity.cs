using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TransBrain.Infrastructure.Persistence.Tenancy;

/// <summary>
/// The shadow <c>tenant_id</c> column that separates tenants inside the shared database.
/// </summary>
/// <remarks>
/// A shadow property, not a domain property: which tenant a row belongs to is an infrastructure
/// concern, and the Domain layer stays tenant-agnostic. <see cref="TransBrainDbContext"/> adds
/// the matching global query filter (it needs the context instance for that, which an
/// <c>IEntityTypeConfiguration</c> never has) and stamps the value on every insert.
/// </remarks>
internal static class TenantEntity
{
    public const string PropertyName = "TenantId";

    public const string ColumnName = "tenant_id";

    /// <summary>
    /// Declares the tenant column plus a plain index on it (every tenant-scoped list query
    /// filters on this column first). Call it before building composite unique indexes with
    /// <see cref="PropertyName"/>.
    /// </summary>
    public static EntityTypeBuilder<TEntity> HasTenantId<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        builder.Property<Guid>(PropertyName).HasColumnName(ColumnName).IsRequired();
        builder.HasIndex(PropertyName);
        return builder;
    }
}
