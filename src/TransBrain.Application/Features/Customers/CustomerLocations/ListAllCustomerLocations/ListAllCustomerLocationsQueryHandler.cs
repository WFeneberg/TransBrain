using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Customers.CustomerLocations.ListAllCustomerLocations;

internal sealed class ListAllCustomerLocationsQueryHandler(ICustomerRepository repository)
    : IQueryHandler<ListAllCustomerLocationsQuery, PagedResult<CustomerLocationListItem>>
{
    public async Task<Result<PagedResult<CustomerLocationListItem>>> Handle(
        ListAllCustomerLocationsQuery query,
        CancellationToken cancellationToken)
    {
        int skip = (query.Page - 1) * query.PageSize;

        IReadOnlyList<CustomerLocationListItem> items =
            await repository.ListAllLocationsAsync(skip, query.PageSize, cancellationToken);
        int totalCount = await repository.CountAllLocationsAsync(cancellationToken);

        return new PagedResult<CustomerLocationListItem>(items, query.Page, query.PageSize, totalCount);
    }
}
