using Microsoft.EntityFrameworkCore;
using Npgsql;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Infrastructure.Persistence.Repositories;

internal sealed class VehicleCatalogRepository(TransBrainDbContext context) : IVehicleCatalogRepository
{
    // PostgreSQL error code for unique_violation.
    private const string UniqueViolation = "23505";

    public async Task<IReadOnlyList<VehicleCatalogEntry>> ListAsync(
        VehicleCatalogKind kind, CancellationToken cancellationToken)
        => await context.VehicleCatalogEntries
            .Where(e => e.Kind == kind)
            .OrderBy(e => e.Name)
            .ThenBy(e => e.Code)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<VehicleCatalogEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => context.VehicleCatalogEntries.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<VehicleCatalogEntry?> FindByCodeAsync(
        VehicleCatalogKind kind, string code, CancellationToken cancellationToken)
    {
        string normalized = code.ToUpperInvariant();
        return context.VehicleCatalogEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Kind == kind && e.Code.ToUpper() == normalized, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, int>> CountUsageAsync(
        VehicleCatalogKind kind, CancellationToken cancellationToken)
    {
        IQueryable<string?> codes = kind == VehicleCatalogKind.VehicleClass
            ? context.Vehicles.Select(v => v.Details.VehicleClass)
            : context.Vehicles.Select(v => v.Details.CarrierGroup);

        return await codes
            .Where(code => code != null)
            .GroupBy(code => code!)
            .Select(group => new { Code = group.Key, Count = group.Count() })
            .ToDictionaryAsync(g => g.Code, g => g.Count, cancellationToken);
    }

    public async Task<Result<VehicleCatalogEntry>> AddAsync(
        VehicleCatalogEntry entry, CancellationToken cancellationToken)
    {
        await context.VehicleCatalogEntries.AddAsync(entry, cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return entry;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolation })
        {
            context.Entry(entry).State = EntityState.Detached;
            return Error.Conflict(
                "VehicleCatalog.DuplicateCode", $"An entry with code '{entry.Code}' already exists.");
        }
    }

    public Task RemoveAsync(VehicleCatalogEntry entry, CancellationToken cancellationToken)
    {
        context.VehicleCatalogEntries.Remove(entry);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => context.SaveChangesAsync(cancellationToken);
}