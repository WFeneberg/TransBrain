using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.VehicleCatalog;

/// <param name="UsageCount">Number of vehicles that currently use this entry.</param>
public sealed record VehicleCatalogEntryResponse(
    Guid Id,
    string Kind,
    string Code,
    string Name,
    bool IsActive,
    int UsageCount)
{
    public static VehicleCatalogEntryResponse From(VehicleCatalogEntry entry, int usageCount) => new(
        entry.Id,
        entry.Kind.ToString(),
        entry.Code,
        entry.Name,
        entry.IsActive,
        usageCount);
}