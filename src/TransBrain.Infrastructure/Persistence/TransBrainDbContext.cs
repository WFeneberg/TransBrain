using Microsoft.EntityFrameworkCore;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Infrastructure.Persistence;

public sealed class TransBrainDbContext(DbContextOptions<TransBrainDbContext> options) : DbContext(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<Driver> Drivers => Set<Driver>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TransBrainDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
