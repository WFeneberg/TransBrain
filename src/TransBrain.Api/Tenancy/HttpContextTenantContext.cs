using System.Security.Claims;
using TransBrain.Application.Abstractions;

namespace TransBrain.Api.Tenancy;

/// <summary>
/// Resolves the caller's tenant (Mandant) from the validated JWT — or, on the cross-tenant
/// system endpoints, from the <see cref="TenantOverride"/> their route group set. Registered
/// scoped, because HttpContext is.
/// </summary>
/// <remarks>
/// The claim comes out of the signed access token Keycloak issues (a <c>tenant_id</c> user
/// attribute mapped into the token, see <c>docs/KEYCLOAK.md</c>), never out of anything a caller
/// can set freely, such as a header. The override wins over the claim: it exists only on the
/// system-admin endpoints, where the operated-on tenant is route state, not caller identity.
/// Fails closed: no resolvable tenant means an exception, not a fallback tenant —
/// <see cref="TenantRequirementMiddleware"/> turns the missing claim into a clean 403 before any
/// handler runs, so this throw only guards non-HTTP misuse.
/// </remarks>
internal sealed class HttpContextTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public const string ClaimType = "tenant_id";

    public Guid TenantId =>
        TenantOverride.TryGet(accessor.HttpContext, out Guid overridden)
            ? overridden
            : TryResolve(accessor.HttpContext?.User, out Guid tenantId)
                ? tenantId
                : throw new InvalidOperationException(
                    $"No tenant could be resolved: the current principal carries no valid '{ClaimType}' claim.");

    /// <summary>Shared with the middleware so "valid tenant" means exactly one thing.</summary>
    internal static bool TryResolve(ClaimsPrincipal? principal, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        string? value = principal?.FindFirstValue(ClaimType);
        return !string.IsNullOrWhiteSpace(value)
               && Guid.TryParse(value, out tenantId)
               && tenantId != Guid.Empty;
    }
}
