using FluentValidation;

namespace TransBrain.Application.Features.Tours.CreateTour;

/// <remarks>
/// Shape only. Whether the vehicle is available, whether the driver's licence covers the tour
/// date, and whether either is already booked that day are domain and database questions —
/// see Tour.Create and TourConfiguration. Restating them here would be a second copy that
/// eventually disagrees with the first.
/// </remarks>
public sealed class CreateTourCommandValidator : AbstractValidator<CreateTourCommand>
{
    public CreateTourCommandValidator()
    {
        RuleFor(c => c.VehicleId).NotEmpty();
        RuleFor(c => c.DriverId).NotEmpty();
    }
}
