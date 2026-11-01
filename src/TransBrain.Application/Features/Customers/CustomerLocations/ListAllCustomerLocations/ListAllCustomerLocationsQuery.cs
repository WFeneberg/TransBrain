using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;

namespace TransBrain.Application.Features.Customers.CustomerLocations.ListAllCustomerLocations;

public sealed record ListAllCustomerLocationsQuery(int Page = 1, int PageSize = 100)
    : IQuery<PagedResult<CustomerLocationListItem>>;
