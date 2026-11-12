using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TransBrain.Application.Abstractions;

namespace TransBrain.Api.IntegrationTests;

/// <summary>
/// Test replacement for the Api's claim-based tenant context. For HTTP requests it behaves like
/// production (reads the <c>tenant_id</c> claim the <see cref="TestAuthHandler"/> issued); for
/// bare DI scopes without an HttpContext — the number-generator tests — it falls back to the
/// default tenant instead of failing closed, because those tests have no principal to carry a
/// claim.
/// </summary>
internal sealed class TestTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public Guid TenantId
    {
        get
        {
            string? value = accessor.HttpContext?.User.FindFirstValue("tenant_id");
            return value is not null && Guid.TryParse(value, out Guid tenantId) && tenantId != Guid.Empty
                ? tenantId
                : TenantDefaults.DefaultTenantId;
        }
    }
}
