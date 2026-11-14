using TransBrain.Domain.Billing;

namespace TransBrain.Application.Abstractions;

/// <summary>
/// The current tenant's invoice design (letterhead data plus layout choices). At most one design
/// row exists per tenant; tenants that never saved one render their documents with the
/// deployment's configured default seller data.
/// </summary>
public interface IInvoiceDesignRepository
{
    /// <summary>The tenant's saved design, tracked for update, or null when none was saved yet.</summary>
    Task<InvoiceDesign?> FindAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The design invoice documents actually render with: the saved design when one exists,
    /// otherwise a transient default built from the deployment's configured seller data.
    /// </summary>
    Task<InvoiceDesign> GetEffectiveAsync(CancellationToken cancellationToken);

    Task AddAsync(InvoiceDesign design, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
