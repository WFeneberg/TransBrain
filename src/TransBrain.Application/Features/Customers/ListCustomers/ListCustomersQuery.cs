using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;

namespace TransBrain.Application.Features.Customers.ListCustomers;

public sealed record ListCustomersQuery(int Page = 1, int PageSize = 20) : IQuery<PagedResult<CustomerResponse>>;
