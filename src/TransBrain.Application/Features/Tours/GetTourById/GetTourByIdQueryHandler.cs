using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Tours.GetTourById;

internal sealed class GetTourByIdQueryHandler(
    ITourRepository tours,
    IVehicleRepository vehicles,
    IDriverRepository drivers,
    ITransportOrderRepository orders,
    ICurrentUser currentUser)
    : IQueryHandler<GetTourByIdQuery, TourResponse>
{
    public async Task<Result<TourResponse>> Handle(
        GetTourByIdQuery query,
        CancellationToken cancellationToken)
    {
        Result<TourContext> context = await TourLoader.LoadAsync(
            query.Id, tours, vehicles, drivers, orders, cancellationToken);

        if (!context.IsSuccess)
        {
            return context.Error!;
        }

        TourContext tour = context.Value;

        // Unlike the list, a single-tour read refuses rather than narrows: the caller asked for
        // one specific tour, and silently answering about a different one would be worse.
        if (!TourAccess.MaySee(tour.Tour, tour.Driver, currentUser))
        {
            return Error.Forbidden("Tour.NotYours", "A driver may only see their own tours.");
        }

        return TourResponse.From(tour.Tour, tour.Vehicle, tour.Driver, tour.AssignedOrders);
    }
}
