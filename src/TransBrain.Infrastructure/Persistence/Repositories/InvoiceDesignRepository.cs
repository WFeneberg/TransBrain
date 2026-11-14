using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;
using TransBrain.Infrastructure.Billing;

namespace TransBrain.Infrastructure.Persistence.Repositories;

internal sealed class InvoiceDesignRepository(
    TransBrainDbContext context,
    IOptions<InvoiceSellerOptions> defaultSeller) : IInvoiceDesignRepository
{
    // No predicate needed: the tenant query filter scopes the set, and the unique tenant index
    // guarantees at most one row inside it. Tracked, because updates mutate the entity.
    public Task<InvoiceDesign?> FindAsync(CancellationToken cancellationToken)
        => context.InvoiceDesigns.SingleOrDefaultAsync(cancellationToken);

    public async Task<InvoiceDesign> GetEffectiveAsync(CancellationToken cancellationToken)
        => await FindAsync(cancellationToken) ?? DefaultDesign();

    public async Task AddAsync(InvoiceDesign design, CancellationToken cancellationToken)
        => await context.InvoiceDesigns.AddAsync(design, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => context.SaveChangesAsync(cancellationToken);

    /// <summary>
    /// The transient bootstrap design for tenants that never saved one, built from the
    /// "InvoiceSeller" configuration section. Deliberately not persisted: a tenant only gets a
    /// row once someone actually saves the designer.
    /// </summary>
    private InvoiceDesign DefaultDesign()
    {
        InvoiceSellerOptions seller = defaultSeller.Value;
        Result<InvoiceDesign> design = InvoiceDesign.Create(
            seller.Name,
            seller.Street,
            seller.PostalCode,
            seller.City,
            seller.CountryCode,
            seller.VatId,
            seller.TaxNumber,
            seller.Email,
            seller.Phone,
            seller.Iban,
            seller.Bic,
            seller.BankName,
            InvoiceDesign.DefaultAccentColor,
            InvoiceDesign.DefaultFontSizePoints,
            InvoiceLogoPosition.Right,
            logo: null,
            logoContentType: null,
            headerText: null,
            footerText: null,
            DateTimeOffset.UnixEpoch);

        // Fail loud, not with an unusable document: an invoice without a proper sender is not a
        // commercial document, so a broken deployment default must surface as an error.
        return design.IsSuccess
            ? design.Value
            : throw new InvalidOperationException(
                $"The configured '{InvoiceSellerOptions.SectionName}' defaults are incomplete: {design.Error!.Message}");
    }
}
