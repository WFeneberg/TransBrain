using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Tours;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Tours.CreateTour;

internal sealed class CreateTourCommandHandler(
    ITourRepository tours,
    IVehicleRepository vehicles,
    IDriverRepository drivers)
    : ICommandHandler<CreateTourCommand, TourResponse>
{
    public async Task<Result<TourResponse>> Handle(
        CreateTourCommand command,
        CancellationToken cancellationToken)
    {
        Vehicle? vehicle = await vehicles.GetByIdAsync(command.VehicleId, cancellationToken);
        if (vehicle is null)
        {
            return Error.NotFound("Vehicle.NotFound", $"No vehicle with id '{command.VehicleId}'.");
        }

        Driver? driver = await drivers.GetByIdAsync(command.DriverId, cancellationToken);
        if (driver is null)
        {
            return Error.NotFound("Driver.NotFound", $"No driver with id '{command.DriverId}'.");
        }

        // Availability and the licence rule are decided here, by the domain, with both objects
        // in hand. Double-booking is decided by the database inside AddAsync below.
        Result<Tour> tour = Tour.Create(command.TourDate, vehicle, driver);
        if (!tour.IsSuccess)
        {
            return tour.Error!;
        }

        Result<Tour> added = await tours.AddAsync(tour.Value, cancellationToken);
        if (!added.IsSuccess)
        {
            return added.Error!;
        }

        return TourResponse.From(added.Value, vehicle, driver, []);
    }
}
