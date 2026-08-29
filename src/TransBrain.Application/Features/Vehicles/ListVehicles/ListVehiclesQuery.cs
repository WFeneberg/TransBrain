using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;

namespace TransBrain.Application.Features.Vehicles.ListVehicles;

public sealed record ListVehiclesQuery(int Page = 1, int PageSize = 20, string? Status = null, string? Type = null)
    : IQuery<PagedResult<VehicleResponse>>;
