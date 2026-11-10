using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.VehicleCatalog.ListVehicleCatalog;

internal sealed class ListVehicleCatalogQueryHandler(IVehicleCatalogRepository repository)
    : IQueryHandler<ListVehicleCatalogQuery, IReadOnlyList<VehicleCatalogEntryResponse>>
{
    public async Task<Result<IReadOnlyList<VehicleCatalogEntryResponse>>> Handle(
        ListVehicleCatalogQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<VehicleCatalogEntry> entries = await repository.ListAsync(query.Kind, cancellationToken);
        IReadOnlyDictionary<string, int> usage = await repository.CountUsageAsync(query.Kind, cancellationToken);

        VehicleCatalogEntryResponse[] items = entries
            .Select(entry => VehicleCatalogEntryResponse.From(entry, usage.GetValueOrDefault(entry.Code)))
            .ToArray();

        return items;
    }
}