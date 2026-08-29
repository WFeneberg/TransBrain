using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;

namespace TransBrain.Application.Features.Drivers.ListDrivers;

public sealed record ListDriversQuery(int Page = 1, int PageSize = 20, string? Status = null)
    : IQuery<PagedResult<DriverResponse>>;
