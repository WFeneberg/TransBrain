using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Fakes;

/// <param name="vehicles">Source of the usage counts; without it every entry counts as unused.</param>
public sealed class InMemoryVehicleCatalogRepository(InMemoryVehicleRepository? vehicles = null)
    : IVehicleCatalogRepository
{
    private readonly List<VehicleCatalogEntry> _entries = [];

    public IReadOnlyList<VehicleCatalogEntry> Entries => _entries;

    public int SaveChangesCallCount { get; private set; }

    /// <summary>A catalog with a few active vehicle classes and carrier groups, as most vehicle tests need.</summary>
    public static InMemoryVehicleCatalogRepository WithStandardEntries(InMemoryVehicleRepository? vehicles = null)
    {
        InMemoryVehicleCatalogRepository catalog = new(vehicles);
        catalog.Seed(VehicleCatalogKind.VehicleClass, "Refrigerated", "Kühlkoffer");
        catalog.Seed(VehicleCatalogKind.VehicleClass, "Tank", "Tank");
        catalog.Seed(VehicleCatalogKind.VehicleClass, "Tarpaulin", "Plane");
        catalog.Seed(VehicleCatalogKind.CarrierGroup, "Nahverkehr", "Nahverkehr Süd");
        catalog.Seed(VehicleCatalogKind.CarrierGroup, "Fernverkehr", "Fernverkehr");
        return catalog;
    }

    public VehicleCatalogEntry Seed(VehicleCatalogKind kind, string code, string name, bool isActive = true)
    {
        VehicleCatalogEntry entry = VehicleCatalogEntry.Create(kind, code, name, isActive).Value;
        _entries.Add(entry);
        return entry;
    }

    public Task<IReadOnlyList<VehicleCatalogEntry>> ListAsync(
        VehicleCatalogKind kind, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<VehicleCatalogEntry>>(
            _entries
                .Where(e => e.Kind == kind)
                .OrderBy(e => e.Name, StringComparer.Ordinal)
                .ThenBy(e => e.Code, StringComparer.Ordinal)
                .ToList());

    public Task<VehicleCatalogEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_entries.SingleOrDefault(e => e.Id == id));

    public Task<VehicleCatalogEntry?> FindByCodeAsync(
        VehicleCatalogKind kind, string code, CancellationToken cancellationToken)
        => Task.FromResult(_entries.FirstOrDefault(
            e => e.Kind == kind && string.Equals(e.Code, code, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyDictionary<string, int>> CountUsageAsync(
        VehicleCatalogKind kind, CancellationToken cancellationToken)
    {
        IEnumerable<string?> codes = (vehicles?.Vehicles ?? []).Select(v =>
            kind == VehicleCatalogKind.VehicleClass ? v.Details.VehicleClass : v.Details.CarrierGroup);

        IReadOnlyDictionary<string, int> usage = codes
            .OfType<string>()
            .GroupBy(code => code)
            .ToDictionary(group => group.Key, group => group.Count());

        return Task.FromResult(usage);
    }

    public Task<Result<VehicleCatalogEntry>> AddAsync(VehicleCatalogEntry entry, CancellationToken cancellationToken)
    {
        _entries.Add(entry);
        return Task.FromResult(Result<VehicleCatalogEntry>.Success(entry));
    }

    public Task RemoveAsync(VehicleCatalogEntry entry, CancellationToken cancellationToken)
    {
        _entries.Remove(entry);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}