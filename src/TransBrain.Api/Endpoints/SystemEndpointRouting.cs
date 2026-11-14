using TransBrain.Api.Authorization;
using TransBrain.Api.Tenancy;

namespace TransBrain.Api.Endpoints;

/// <summary>
/// Routing helpers for the cross-tenant system area (<c>/api/system</c>). Every group created
/// here demands the <c>system-admin</c> role and bypasses the tenant-claim gate (system
/// administrators deliberately belong to no tenant); groups nested under
/// <c>/tenants/{tenantId}</c> additionally pin the ambient tenant to the route value, so every
/// tenant-scoped service (query filters, number generators, cache keys, backup/restore) runs
/// against the chosen tenant without knowing it was chosen.
/// </summary>
internal static class SystemEndpointRouting
{
    /// <summary>Group under <c>/api/system</c>: system-admin only, no tenant claim required.</summary>
    public static RouteGroupBuilder MapSystemGroup(this IEndpointRouteBuilder app)
        => app.MapGroup("/api/system")
            .RequireAuthorization(Policies.SystemMaintenance)
            .WithMetadata(new SkipTenantRequirementAttribute());

    /// <summary>
    /// Group under <c>/api/system/tenants/{tenantId:guid}<paramref name="suffix"/></c> whose
    /// endpoints all run scoped to the tenant named in the route.
    /// </summary>
    public static RouteGroupBuilder MapSystemTenantGroup(this IEndpointRouteBuilder app, string suffix = "")
        => app.MapSystemGroup()
            .MapGroup("/tenants/{tenantId:guid}" + suffix)
            .AddEndpointFilter(ScopeToRouteTenant);

    /// <summary>
    /// Runs after authorization (endpoint filters always do): only a proven system-admin ever
    /// reaches the override. The route constraint already guarantees a well-formed Guid; the
    /// empty Guid is rejected here because no tenant may ever own it.
    /// </summary>
    private static async ValueTask<object?> ScopeToRouteTenant(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        HttpContext http = context.HttpContext;
        if (!Guid.TryParse(http.Request.RouteValues["tenantId"]?.ToString(), out Guid tenantId)
            || tenantId == Guid.Empty)
        {
            return Results.Problem(
                title: "SystemTenant.Invalid",
                detail: "The tenant id in the route must be a non-empty GUID.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        TenantOverride.Set(http, tenantId);
        return await next(context);
    }
}
