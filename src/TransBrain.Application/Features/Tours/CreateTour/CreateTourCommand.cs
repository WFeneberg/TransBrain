using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Tours.CreateTour;

public sealed record CreateTourCommand(DateOnly TourDate, Guid VehicleId, Guid DriverId)
    : ICommand<TourResponse>;
