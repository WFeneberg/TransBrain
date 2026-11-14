namespace TransBrain.Application.Abstractions;

/// <summary>
/// Read-only view over which tenants (Mandanten) actually hold data in this database — the
/// system administrator's picklist on the global maintenance page. Implemented in
/// Infrastructure, because "which tenants exist" is a question about physical rows, not about
/// any aggregate: the system has no tenant master table, a tenant exists by owning data.
/// </summary>
public interface ITenantDirectory
{
    /// <summary>All tenants owning at least one row, with headline counts, ordered by id.</summary>
    Task<IReadOnlyList<TenantSummary>> ListAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Headline numbers of one tenant's data set. <paramref name="TotalRows"/> spans every
/// tenant-owned table (the same set a backup copies), the named counts are the ones worth
/// showing next to the tenant picker.
/// </summary>
public sealed record TenantSummary(
    Guid TenantId,
    int Customers,
    int Drivers,
    int Vehicles,
    int Orders,
    int Tours,
    int Invoices,
    int TotalRows);
