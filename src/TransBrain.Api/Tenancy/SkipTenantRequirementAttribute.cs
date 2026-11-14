namespace TransBrain.Api.Tenancy;

/// <summary>
/// Endpoint metadata that exempts an endpoint from <see cref="TenantRequirementMiddleware"/>'s
/// "authenticated callers must carry a tenant" gate. Reserved for the cross-tenant system
/// endpoints: a system administrator is deliberately not assigned to any tenant, and the tenant
/// an operation affects comes from the route (see <see cref="TenantOverride"/>), not the token.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SkipTenantRequirementAttribute : Attribute;
