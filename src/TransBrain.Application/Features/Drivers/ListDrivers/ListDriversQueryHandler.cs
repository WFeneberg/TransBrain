using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers.ListDrivers;

internal sealed class ListDriversQueryHandler(IDriverRepository repository)
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

        int skip = (query.Page - 1) * query.PageSize;

        IReadOnlyList<Driver> drivers = await repository.ListAsync(skip, query.PageSize, status, cancellationToken);
        int totalCount = await repository.CountAsync(status, cancellationToken);

        DriverResponse[] items = drivers.Select(DriverResponse.From).ToArray();

        return new PagedResult<DriverResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
