namespace TransBrain.Application.Abstractions;

/// <summary>
/// The authenticated caller, as far as the Application layer needs to know them.
/// </summary>
/// <remarks>
/// Spec §9 restricts a driver to their own tours, and that rule has to be checked where the
/// tour and the driver are both in hand — in a handler. Handlers must not reference
/// HttpContext or ClaimsPrincipal, so this is the seam. <see cref="UserId"/> is the Keycloak
/// "sub" claim, which is what a driver's <c>ExternalUserId</c> stores.
/// </remarks>
public interface ICurrentUser
{
    string? UserId { get; }

    bool IsInRole(string role);
}
