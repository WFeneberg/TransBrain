using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Customers;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Imports;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Routing;
using TransBrain.Domain.Tours;
using TransBrain.Domain.Vehicles;
using TransBrain.Infrastructure.Persistence.Imports;
using TransBrain.Infrastructure.Persistence.Tenancy;

namespace TransBrain.Infrastructure.Persistence;

public sealed class TransBrainDbContext(
    DbContextOptions<TransBrainDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<VehicleCatalogEntry> VehicleCatalogEntries => Set<VehicleCatalogEntry>();

    public DbSet<VehicleDocument> VehicleDocuments => Set<VehicleDocument>();

    public DbSet<Driver> Drivers => Set<Driver>();

    public DbSet<TransportOrder> TransportOrders => Set<TransportOrder>();

    public DbSet<Tour> Tours => Set<Tour>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerLocation> CustomerLocations => Set<CustomerLocation>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<GeoOverride> GeoOverrides => Set<GeoOverride>();

    public DbSet<ImportSession> ImportSessions => Set<ImportSession>();

    /// <summary>
    /// The tenant the current scope runs for. Referenced from the query filters below, which EF
    /// re-evaluates per query, so one cached model serves every tenant. Deliberately lazy:
    /// resolution may throw (fail closed, see <see cref="ITenantContext"/>), and that must happen
    /// when tenant data is actually touched, not when the context is constructed — migrations and
    /// health checks create this context without any tenant.
    /// </summary>
    private Guid CurrentTenantId => tenantContext.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TransBrainDbContext).Assembly);

        // Owned types (tour stops, invoice lines, location contacts, ...) carry no filter of
        // their own: they are only reachable through their owner, whose filter already applies.
        ApplyTenantFilter<Vehicle>(modelBuilder);
        ApplyTenantFilter<VehicleCatalogEntry>(modelBuilder);
        ApplyTenantFilter<VehicleDocument>(modelBuilder);
        ApplyTenantFilter<Driver>(modelBuilder);
        ApplyTenantFilter<TransportOrder>(modelBuilder);
        ApplyTenantFilter<Tour>(modelBuilder);
        ApplyTenantFilter<Customer>(modelBuilder);
        ApplyTenantFilter<CustomerLocation>(modelBuilder);
        ApplyTenantFilter<Invoice>(modelBuilder);
        ApplyTenantFilter<GeoOverride>(modelBuilder);
        ApplyTenantFilter<ImportSession>(modelBuilder);
        ApplyTenantFilter<ImportSessionData>(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTenant();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampTenant();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder) where TEntity : class
        => modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e => EF.Property<Guid>(e, TenantEntity.PropertyName) == CurrentTenantId);

    /// <summary>
    /// Stamps every inserted tenant-owned row with the current tenant, unconditionally: rows are
    /// never written on another tenant's behalf, and the global query filters guarantee no
    /// foreign row can be tracked for update or delete in the first place.
    /// </summary>
    private void StampTenant()
    {
        Guid? tenantId = null;

        foreach (EntityEntry entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added
                || entry.Metadata.FindProperty(TenantEntity.PropertyName) is null)
            {
                continue;
            }

            // Resolved on first use only: saves that touch no tenant-owned rows must stay
            // possible without a tenant, and a missing tenant must fail the save (fail closed).
            tenantId ??= CurrentTenantId;
            entry.Property(TenantEntity.PropertyName).CurrentValue = tenantId;
        }
    }
}
