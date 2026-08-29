using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Tours;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Tours;

/// <summary>A tour together with everything needed to decide about it and to render it.</summary>
internal sealed record TourContext(
    Tour Tour,
    Vehicle Vehicle,
    Driver Driver,
    IReadOnlyList<TransportOrder> AssignedOrders);

/// <remarks>
/// Every tour handler needs the same four loads: the tour, its vehicle, its driver, and the
/// orders its stops point at. Written once here rather than five times, because five copies of
/// four not-found branches is where one branch quietly goes missing.
/// </remarks>
internal static class TourLoader
{
    public static async Task<Result<TourContext>> LoadAsync(
        Guid tourId,
        ITourRepository tours,
        IVehicleRepository vehicles,
        IDriverRepository drivers,
        ITransportOrderRepository orders,
        CancellationToken cancellationToken)
    {
        Tour? tour = await tours.GetByIdAsync(tourId, cancellationToken);
        if (tour is null)
        {
            return Error.NotFound("Tour.NotFound", $"No tour with id '{tourId}'.");
        }

        Vehicle? vehicle = await vehicles.GetByIdAsync(tour.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            // Only reachable if a vehicle was deleted out from under a tour. Reported rather
            // than dereferenced, so the cause is legible instead of a NullReferenceException.
            return Error.NotFound("Vehicle.NotFound", $"No vehicle with id '{tour.VehicleId}'.");
        }

        Driver? driver = await drivers.GetByIdAsync(tour.DriverId, cancellationToken);
        if (driver is null)
        {
            return Error.NotFound("Driver.NotFound", $"No driver with id '{tour.DriverId}'.");
        }

        List<TransportOrder> assigned = [];
        foreach (Guid orderId in tour.AssignedOrderIds())
        {
            TransportOrder? order = await orders.GetByIdAsync(orderId, cancellationToken);
            if (order is not null)
            {
                assigned.Add(order);
            }
        }

        return new TourContext(tour, vehicle, driver, assigned);
    }
}
