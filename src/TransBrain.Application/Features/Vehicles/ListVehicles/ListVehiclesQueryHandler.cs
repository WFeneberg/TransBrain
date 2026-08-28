using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.ListVehicles;

internal sealed class ListVehiclesQueryHandler(IVehicleRepository repository)
    : IQueryHandler<ListVehiclesQuery, PagedResult<VehicleResponse>>
{
    public async Task<Result<PagedResult<VehicleResponse>>> Handle(
        ListVehiclesQuery query,
        CancellationToken cancellationToken)
    {
        int skip = (query.Page - 1) * query.PageSize;

        IReadOnlyList<Vehicle> vehicles = await repository.ListAsync(skip, query.PageSize, cancellationToken);
        int totalCount = await repository.CountAsync(cancellationToken);

        VehicleResponse[] items = vehicles.Select(VehicleResponse.From).ToArray();

        return new PagedResult<VehicleResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
