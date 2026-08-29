using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Tours;

namespace TransBrain.Application.Features.Tours.ListTours;

/// <remarks>
/// Deliberately not cached: spec §7 excludes tours along with orders, as too volatile for the
/// invalidation cost.
/// </remarks>
internal sealed class ListToursQueryHandler(
    ITourRepository tours,
    IVehicleRepository vehicles,
    IDriverRepository drivers,
    ITransportOrderRepository orders,
    ICurrentUser currentUser)
    : IQueryHandler<ListToursQuery, PagedResult<TourResponse>>
{
    public async Task<Result<PagedResult<TourResponse>>> Handle(
        ListToursQuery query,
        CancellationToken cancellationToken)
    {
        Guid? driverFilter = query.DriverId;

        // Spec §9: a fahrer sees only their own tours. Narrowed rather than refused - a list
        // endpoint that 403s would be useless to a driver opening the screen.
        //
        // The caller's scope is INTERSECTED with what they asked for, not substituted for it.
        // Overwriting would answer a different question than the one asked: a driver who filters
        // on a colleague's id would get their own tours back, looking like the filter had worked.
        // An empty page is the truthful answer to "this colleague's tours, among mine".
        if (TourAccess.IsDriverOnly(currentUser))
        {
            Driver? me = await FindDriverForCallerAsync(cancellationToken);

            // No driver record bound to this login means no tours are theirs - and must not
            // fall through to an unfiltered list.
            if (me is null || (query.DriverId is not null && query.DriverId != me.Id))
            {
                return new PagedResult<TourResponse>([], query.Page, query.PageSize, 0);
            }

            driverFilter = me.Id;
        }

        int skip = (query.Page - 1) * query.PageSize;

        IReadOnlyList<Tour> page = await tours.ListAsync(
            skip, query.PageSize, query.TourDate, query.VehicleId, driverFilter, cancellationToken);

        int totalCount = await tours.CountAsync(
            query.TourDate, query.VehicleId, driverFilter, cancellationToken);

        List<TourResponse> items = [];
        foreach (Tour tour in page)
        {
            Result<TourContext> context = await TourLoader.LoadAsync(
                tour.Id, tours, vehicles, drivers, orders, cancellationToken);

            if (context.IsSuccess)
            {
                items.Add(TourResponse.From(
                    context.Value.Tour, context.Value.Vehicle, context.Value.Driver,
                    context.Value.AssignedOrders));
            }
        }

        return new PagedResult<TourResponse>(items, query.Page, query.PageSize, totalCount);
    }

    private async Task<Driver?> FindDriverForCallerAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            return null;
        }

        return await drivers.GetByExternalUserIdAsync(currentUser.UserId, cancellationToken);
    }
}
