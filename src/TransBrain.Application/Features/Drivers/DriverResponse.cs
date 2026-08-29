using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers;

public sealed record DriverResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string[] LicenseClasses,
    DateOnly LicenseValidUntil,
    string Status,
    string? ExternalUserId)
{
    public static DriverResponse From(Driver driver) => new(
        driver.Id,
        driver.FirstName,
        driver.LastName,
        driver.LicenseClasses.Select(c => c.ToString()).ToArray(),
        driver.LicenseValidUntil,
        driver.Status.ToString(),
        driver.ExternalUserId);
}
