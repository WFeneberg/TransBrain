using Microsoft.EntityFrameworkCore;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Infrastructure.Persistence;

public sealed class TransBrainDbContext(DbContextOptions<TransBrainDbContext> options) : DbContext(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<Driver> Drivers => Set<Driver>();

    public DbSet<TransportOrder> TransportOrders => Set<TransportOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TransBrainDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
