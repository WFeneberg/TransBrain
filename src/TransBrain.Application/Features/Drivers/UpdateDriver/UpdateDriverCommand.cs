using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Drivers.UpdateDriver;

public sealed record UpdateDriverCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string[] LicenseClasses,
    DateOnly LicenseValidUntil,
    string? ExternalUserId) : ICommand<DriverResponse>;
