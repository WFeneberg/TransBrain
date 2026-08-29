using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Tours;

namespace TransBrain.Application.Features.Tours;

/// <summary>
/// Spec §9's "nur eigene" rule for drivers, written exactly once.
/// </summary>
/// <remarks>
/// A driver's identity is the Keycloak "sub" claim, which is stored on the driver record as
/// ExternalUserId. A tour whose driver has no ExternalUserId therefore belongs to nobody who
/// can sign in, and a fahrer is refused it — treating a missing link as "matches everyone"
/// would hand every unlinked driver's tour to whoever asked first.
/// </remarks>
internal static class TourAccess
{
    public const string AdminRole = "admin";
    public const string DispatcherRole = "disponent";
    public const string DriverRole = "fahrer";

    public static Result<Unit> EnsureMayChangeStatus(TourContext context, ICurrentUser currentUser)
    {
        if (MaySee(context.Tour, context.Driver, currentUser))
        {
            return Unit.Value;
        }

        return Error.Forbidden(
            "Tour.NotYours",
            "A driver may only start or complete their own tours.");
    }

    public static bool MaySee(Tour tour, Driver driver, ICurrentUser currentUser)
    {
        if (currentUser.IsInRole(AdminRole) || currentUser.IsInRole(DispatcherRole))
        {
            return true;
        }

        return driver.Id == tour.DriverId
               && !string.IsNullOrWhiteSpace(driver.ExternalUserId)
               && !string.IsNullOrWhiteSpace(currentUser.UserId)
               && string.Equals(driver.ExternalUserId, currentUser.UserId, StringComparison.Ordinal);
    }
}
