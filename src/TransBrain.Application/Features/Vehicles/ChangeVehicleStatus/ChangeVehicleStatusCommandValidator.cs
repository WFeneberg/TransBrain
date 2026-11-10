using FluentValidation;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.ChangeVehicleStatus;

public sealed class ChangeVehicleStatusCommandValidator : AbstractValidator<ChangeVehicleStatusCommand>
{
    public ChangeVehicleStatusCommandValidator()
    {
        RuleFor(c => c.Status)
            .NotEmpty()
            .Must(status => ChangeVehicleStatusCommandHandler.TryParseStatus(status, out _))
            .WithMessage(c => $"'{c.Status}' is not a known vehicle status. Allowed: {string.Join(", ", Enum.GetNames<VehicleStatus>())}.");
    }
}
