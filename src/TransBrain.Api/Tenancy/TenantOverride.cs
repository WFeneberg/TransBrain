using Microsoft.AspNetCore.Http;

namespace TransBrain.Api.Tenancy;

/// <summary>
/// The per-request tenant override the cross-tenant system endpoints use: a system administrator
/// carries no <c>tenant_id</c> claim, so the affected tenant comes from the route instead.
/// </summary>
/// <remarks>
/// The override lives in <see cref="HttpContext.Items"/> and is set exclusively by the
/// <c>/api/system</c> route group's endpoint filter — which runs only after the
/// <c>SystemMaintenance</c> policy has passed. A regular caller can therefore never smuggle an
/// override in: nothing outside that group writes the key, and Items is not caller-controlled.
/// Public (not internal) so the integration tests' tenant context can honor the same override.
/// </remarks>
public static class TenantOverride
{
    private const string ItemKey = "transbrain:tenant-override";

    public static void Set(HttpContext context, Guid tenantId) => context.Items[ItemKey] = tenantId;

    public static bool TryGet(HttpContext? context, out Guid tenantId)
    {
        if (context?.Items.TryGetValue(ItemKey, out object? value) == true && value is Guid stored)
        {
            tenantId = stored;
            return true;
        }

        tenantId = Guid.Empty;
        return false;
    }
}
