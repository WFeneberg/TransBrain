using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;

namespace TransBrain.Application.Features.Customers.ListCustomers;

internal sealed class ListCustomersQueryHandler(ICustomerRepository repository, ICacheService cache)
    : IQueryHandler<ListCustomersQuery, PagedResult<CustomerResponse>>
{
    public async Task<Result<PagedResult<CustomerResponse>>> Handle(
        ListCustomersQuery query,
        CancellationToken cancellationToken)
    {
        // Read before the database or the cache, and folded into the key: see the matching
        // comment in ListDriversQueryHandler and ICacheService.GetGenerationAsync.
        long generation = await cache.GetGenerationAsync(CustomerCacheKeys.Prefix, cancellationToken);
        string cacheKey = $"{CustomerCacheKeys.Prefix}list:{generation}:{query.Page}:{query.PageSize}";

        PagedResult<CustomerResponse>? cached =
            await cache.GetAsync<PagedResult<CustomerResponse>>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        int skip = (query.Page - 1) * query.PageSize;

        IReadOnlyList<Customer> customers = await repository.ListAsync(skip, query.PageSize, cancellationToken);
        int totalCount = await repository.CountAsync(cancellationToken);

        CustomerResponse[] items = customers.Select(CustomerResponse.From).ToArray();

        PagedResult<CustomerResponse> result = new(items, query.Page, query.PageSize, totalCount);

        await cache.SetAsync(cacheKey, result, cancellationToken);

        return result;
    }
}
