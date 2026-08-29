using TransBrain.Domain.Drivers;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Tours;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Tours;

public sealed record TourStopResponse(int Sequence, Guid TransportOrderId, string OrderNumber, string StopType);

public sealed record TourResponse(
    Guid Id,
    DateOnly TourDate,
    Guid VehicleId,
    string VehicleLicensePlate,
    Guid DriverId,
    string DriverName,
    string Status,
    int TotalWeightKg,
    decimal TotalLoadMeters,
    int VehiclePayloadKg,
    decimal VehicleLoadMeters,
    IReadOnlyList<TourStopResponse> Stops)
{
    /// <param name="assignedOrders">
    /// Every order this tour's stops refer to. Carried so the response can report the tour's
    /// load against the vehicle's rating and name each stop's order — a dispatcher deciding
    /// whether one more order fits needs both numbers, and fetching the vehicle separately to
    /// draw a capacity bar would be a round trip for data the server already held.
    /// </param>
    public static TourResponse From(
        Tour tour,
        Vehicle vehicle,
        Driver driver,
        IReadOnlyList<TransportOrder> assignedOrders)
    {
        Dictionary<Guid, TransportOrder> byId = assignedOrders.ToDictionary(order => order.Id);

        TourStopResponse[] stops = tour.Stops
            .OrderBy(stop => stop.Sequence)
            .Select(stop => new TourStopResponse(
                stop.Sequence,
                stop.TransportOrderId,
                // An id with no order behind it means the two were loaded inconsistently.
                // Showing the raw id is more useful to whoever debugs that than an exception.
                byId.TryGetValue(stop.TransportOrderId, out TransportOrder? order)
                    ? order.OrderNumber.Value
                    : stop.TransportOrderId.ToString(),
                stop.StopType.ToString()))
            .ToArray();

        return new TourResponse(
            tour.Id,
            tour.TourDate,
            tour.VehicleId,
            vehicle.LicensePlate.Value,
            tour.DriverId,
            $"{driver.LastName}, {driver.FirstName}",
            tour.Status.ToString(),
            assignedOrders.Sum(order => order.Cargo.WeightKg),
            assignedOrders.Sum(order => order.Cargo.LoadMeters),
            vehicle.PayloadKg,
            vehicle.LoadMeters,
            stops);
    }
}
