using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;

namespace TransBrain.Application.Features.Tours.ListTours;

public sealed record ListToursQuery(
    int Page = 1,
    int PageSize = 20,
    DateOnly? TourDate = null,
    Guid? VehicleId = null,
    Guid? DriverId = null) : IQuery<PagedResult<TourResponse>>;
