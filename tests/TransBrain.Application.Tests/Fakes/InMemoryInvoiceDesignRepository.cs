using TransBrain.Application.Abstractions;
using TransBrain.Application.Tests.Features.InvoiceDesigns;
using TransBrain.Domain.Billing;

namespace TransBrain.Application.Tests.Fakes;

/// <summary>
/// In-memory stand-in for the tenant's single invoice-design row. Mirrors the production
/// contract: FindAsync answers null until something was added, GetEffectiveAsync falls back to
/// a configurable default design.
/// </summary>
internal sealed class InMemoryInvoiceDesignRepository : IInvoiceDesignRepository
{
    public InvoiceDesign? Saved { get; private set; }

    public InvoiceDesign Default { get; set; } = InvoiceDesignFixture.ADesign();

    public int SaveChangesCalls { get; private set; }

    public Task<InvoiceDesign?> FindAsync(CancellationToken cancellationToken)
        => Task.FromResult(Saved);

    public Task<InvoiceDesign> GetEffectiveAsync(CancellationToken cancellationToken)
        => Task.FromResult(Saved ?? Default);

    public Task AddAsync(InvoiceDesign design, CancellationToken cancellationToken)
    {
        Saved = design;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCalls++;
        return Task.CompletedTask;
    }

    public void Seed(InvoiceDesign design) => Saved = design;
}
