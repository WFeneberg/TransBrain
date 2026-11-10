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

    public Task<IReadOnlyDictionary<string, int>> CountUsageAsync(
        VehicleCatalogKind kind, CancellationToken cancellationToken) => kind switch
    {
        VehicleCatalogKind.VehicleClass =>
            CountCodesAsync(context.Vehicles.Select(v => v.Details.VehicleClass), cancellationToken),
        VehicleCatalogKind.CarrierGroup =>
            CountCodesAsync(context.Vehicles.Select(v => v.Details.CarrierGroup), cancellationToken),
        // A vehicle holds each document type at most once, so counting documents counts vehicles.
        VehicleCatalogKind.DocumentType =>
            CountCodesAsync(context.VehicleDocuments.Select(d => (string?)d.DocumentType), cancellationToken),
        VehicleCatalogKind.InsuranceCompany => CountInsurerUsageAsync(cancellationToken),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static async Task<IReadOnlyDictionary<string, int>> CountCodesAsync(
        IQueryable<string?> codes, CancellationToken cancellationToken)
        => await codes
            .Where(code => code != null)
            .GroupBy(code => code!)
            .Select(group => new { Code = group.Key, Count = group.Count() })
            .ToDictionaryAsync(g => g.Code, g => g.Count, cancellationToken);

    // A vehicle insured for both vehicle and transport by the same company counts once.
    private async Task<IReadOnlyDictionary<string, int>> CountInsurerUsageAsync(CancellationToken cancellationToken)
    {
        var insurers = await context.Vehicles
            .Where(v => v.Commercial.VehicleInsurer != null || v.Commercial.TransportInsurer != null)
            .Select(v => new { v.Commercial.VehicleInsurer, v.Commercial.TransportInsurer })
            .ToListAsync(cancellationToken);

        return insurers
            .SelectMany(i => new[] { i.VehicleInsurer, i.TransportInsurer }.Distinct())
            .OfType<string>()
            .GroupBy(code => code, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
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