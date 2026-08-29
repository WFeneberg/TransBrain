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

    /// <summary>
    /// True when the caller is scoped to their own tours: spec §9 narrows the fahrer row and
    /// ONLY that row. A viewer reads everything, which is why this asks for the driver role
    /// rather than merely excluding admin and disponent — an early version excluded, and a
    /// viewer listing tours then saw an empty page.
    /// </summary>
    public static bool IsDriverOnly(ICurrentUser currentUser) =>
        currentUser.IsInRole(DriverRole)
        && !currentUser.IsInRole(AdminRole)
        && !currentUser.IsInRole(DispatcherRole);

    public static bool MaySee(Tour tour, Driver driver, ICurrentUser currentUser)
    {
        if (!IsDriverOnly(currentUser))
        {
            return true;
        }

        return driver.Id == tour.DriverId
               && !string.IsNullOrWhiteSpace(driver.ExternalUserId)
               && !string.IsNullOrWhiteSpace(currentUser.UserId)
               && string.Equals(driver.ExternalUserId, currentUser.UserId, StringComparison.Ordinal);
    }
}
