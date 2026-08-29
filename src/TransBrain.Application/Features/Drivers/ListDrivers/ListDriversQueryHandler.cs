using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers.ListDrivers;

internal sealed class ListDriversQueryHandler(IDriverRepository repository, ICacheService cache)
    : IQueryHandler<ListDriversQuery, PagedResult<DriverResponse>>
{
    public async Task<Result<PagedResult<DriverResponse>>> Handle(
        ListDriversQuery query,
        CancellationToken cancellationToken)
    {
        DriverStatus? status = null;

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            // Enum.TryParse accepts numeric strings, so "99" would otherwise become an
            // undefined enum member and reach the repository. IsDefined closes that gap.
            if (!Enum.TryParse(query.Status, ignoreCase: true, out DriverStatus parsed)
                || !Enum.IsDefined(parsed))
            {
                return Error.Validation("Driver.UnknownStatus", $"'{query.Status}' is not a known driver status.");
            }

            status = parsed;
        }

        // Read before touching the database or the cache, and folded into the key below: this is
        // what closes the cache-aside read-then-set race (see ICacheService.GetGenerationAsync).
        // A write that commits and invalidates between this line and the SetAsync call at the
        // bottom bumps the generation, so this handler's eventual write lands under a value
        // nothing will look up again instead of serving a stale page.
        long generation = await cache.GetGenerationAsync(DriverCacheKeys.Prefix, cancellationToken);

        // Every query parameter must be part of the key: omitting one (e.g. the status filter)
        // would serve one filter combination's cached page under another's request. Built from
        // the parsed enum value (or the "none" literal), not the raw query string, so equivalent
        // requests share one entry: "Absent" and "absent" parse to the same status, and a
        // whitespace-only filter means the same "no filter" as null.
        string cacheKey =
            $"{DriverCacheKeys.Prefix}list:{generation}:{query.Page}:{query.PageSize}:{status?.ToString() ?? "none"}";

        PagedResult<DriverResponse>? cached =
            await cache.GetAsync<PagedResult<DriverResponse>>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        int skip = (query.Page - 1) * query.PageSize;

        IReadOnlyList<Driver> drivers = await repository.ListAsync(skip, query.PageSize, status, cancellationToken);
        int totalCount = await repository.CountAsync(status, cancellationToken);

        DriverResponse[] items = drivers.Select(DriverResponse.From).ToArray();

        PagedResult<DriverResponse> result = new(items, query.Page, query.PageSize, totalCount);

        await cache.SetAsync(cacheKey, result, cancellationToken);

        return result;
    }
}
