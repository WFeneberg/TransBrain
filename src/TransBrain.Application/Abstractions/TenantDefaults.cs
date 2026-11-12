namespace TransBrain.Application.Abstractions;

/// <summary>
/// Well-known tenant ids.
/// </summary>
public static class TenantDefaults
{
    /// <summary>
    /// The tenant every row created before multi-tenancy existed belongs to, and the tenant the
    /// seeded vehicle catalog is created for. The AddMultiTenancy migration back-fills existing
    /// data with this id, and the development realm's test users carry it as their
    /// <c>tenant_id</c> attribute, so a single-tenant installation keeps working unchanged.
    /// </summary>
    public static readonly Guid DefaultTenantId = Guid.Parse("0199c0de-0000-7000-8000-000000000001");
}
