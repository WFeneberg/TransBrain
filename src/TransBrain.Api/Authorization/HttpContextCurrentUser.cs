using System.Security.Claims;
using TransBrain.Application.Abstractions;

namespace TransBrain.Api.Authorization;

/// <summary>
/// Reads the caller out of the current request. Registered scoped, because HttpContext is.
/// </summary>
/// <remarks>
/// "sub" is Keycloak's subject claim and is what a driver's ExternalUserId stores. ASP.NET maps
/// "sub" onto ClaimTypes.NameIdentifier by default, so both are read — relying on only one of
/// them breaks the moment the inbound-claim mapping changes.
/// </remarks>
internal sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string? UserId =>
        accessor.HttpContext?.User.FindFirstValue("sub")
        ?? accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

    public bool IsInRole(string role) => accessor.HttpContext?.User.IsInRole(role) ?? false;
}
