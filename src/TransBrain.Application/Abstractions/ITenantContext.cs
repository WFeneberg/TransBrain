namespace TransBrain.Application.Abstractions;

/// <summary>
/// The tenant (Mandant) the current operation runs for. TransBrain keeps every tenant's data in
/// one shared database, separated row-by-row; this is the seam through which Infrastructure
/// learns which rows belong to the caller.
/// </summary>
/// <remarks>
/// The Api resolves the tenant from the caller's JWT (<c>tenant_id</c> claim) — never from a
/// header or request body a caller could forge. Implementations must fail closed: when no
/// tenant can be resolved, reading <see cref="TenantId"/> throws instead of returning a
/// catch-all value, so a misconfigured caller can never read or write another tenant's rows.
/// </remarks>
public interface ITenantContext
{
    /// <exception cref="InvalidOperationException">No tenant could be resolved.</exception>
    Guid TenantId { get; }
}
