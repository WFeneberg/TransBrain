using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.ListVehicles;

internal sealed class ListVehiclesQueryHandler(IVehicleRepository repository, ICacheService cache)
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

        // Read before touching the database or the cache, and folded into the key below: this is
        // what closes the cache-aside read-then-set race (see ICacheService.GetGenerationAsync).
        // A write that commits and invalidates between this line and the SetAsync call at the
        // bottom bumps the generation, so this handler's eventual write lands under a value
        // nothing will look up again instead of serving a stale page.
        long generation = await cache.GetGenerationAsync(VehicleCacheKeys.Prefix, cancellationToken);

        // Every query parameter must be part of the key: omitting one (e.g. a filter) would
        // serve one filter combination's cached page under another's request. Built from the
        // parsed enum values (or the "none" literal), not the raw query strings, so equivalent
        // requests share one entry: "Available" and "available" parse to the same status, and a
        // whitespace-only filter means the same "no filter" as null - the raw strings would
        // otherwise scatter all of those across distinct, needlessly duplicated cache entries.
        string cacheKey =
            $"{VehicleCacheKeys.Prefix}list:{generation}:{query.Page}:{query.PageSize}:" +
            $"{status?.ToString() ?? "none"}:{type?.ToString() ?? "none"}";

        PagedResult<VehicleResponse>? cached =
            await cache.GetAsync<PagedResult<VehicleResponse>>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        int skip = (query.Page - 1) * query.PageSize;

        IReadOnlyList<Vehicle> vehicles =
            await repository.ListAsync(skip, query.PageSize, status, type, cancellationToken);
        int totalCount = await repository.CountAsync(status, type, cancellationToken);

        VehicleResponse[] items = vehicles.Select(VehicleResponse.From).ToArray();

        PagedResult<VehicleResponse> result = new(items, query.Page, query.PageSize, totalCount);

        await cache.SetAsync(cacheKey, result, cancellationToken);

        return result;
    }
}
