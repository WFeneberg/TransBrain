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
        VehicleStatus? status = null;

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            // Enum.TryParse accepts numeric strings, so "99" would otherwise become an
            // undefined enum member and reach the repository. IsDefined closes that gap.
            if (!Enum.TryParse(query.Status, ignoreCase: true, out VehicleStatus parsedStatus)
                || !Enum.IsDefined(parsedStatus))
            {
                return Error.Validation("Vehicle.UnknownStatus", $"'{query.Status}' is not a known vehicle status.");
            }

            status = parsedStatus;
        }

        VehicleType? type = null;

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            if (!Enum.TryParse(query.Type, ignoreCase: true, out VehicleType parsedType)
                || !Enum.IsDefined(parsedType))
            {
                return Error.Validation("Vehicle.UnknownType", $"'{query.Type}' is not a known vehicle type.");
            }

            type = parsedType;
        }

        int skip = (query.Page - 1) * query.PageSize;

        IReadOnlyList<Vehicle> vehicles =
            await repository.ListAsync(skip, query.PageSize, status, type, cancellationToken);
        int totalCount = await repository.CountAsync(status, type, cancellationToken);

        VehicleResponse[] items = vehicles.Select(VehicleResponse.From).ToArray();

        return new PagedResult<VehicleResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
