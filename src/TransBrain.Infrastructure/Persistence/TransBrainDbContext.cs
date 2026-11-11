using Microsoft.EntityFrameworkCore;
using TransBrain.Domain.Customers;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Routing;
using TransBrain.Domain.Tours;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Infrastructure.Persistence;

public sealed class TransBrainDbContext(DbContextOptions<TransBrainDbContext> options) : DbContext(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<VehicleCatalogEntry> VehicleCatalogEntries => Set<VehicleCatalogEntry>();

    public DbSet<VehicleDocument> VehicleDocuments => Set<VehicleDocument>();

    public DbSet<Driver> Drivers => Set<Driver>();

    public DbSet<TransportOrder> TransportOrders => Set<TransportOrder>();

    public DbSet<Tour> Tours => Set<Tour>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<CustomerLocation> CustomerLocations => Set<CustomerLocation>();

    public DbSet<GeoOverride> GeoOverrides => Set<GeoOverride>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TransBrainDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
