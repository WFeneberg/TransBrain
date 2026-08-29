using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Drivers.CreateDriver;

public sealed record CreateDriverCommand(
    string FirstName,
    string LastName,
    string[] LicenseClasses,
    DateOnly LicenseValidUntil,
    string? ExternalUserId) : ICommand<DriverResponse>;
