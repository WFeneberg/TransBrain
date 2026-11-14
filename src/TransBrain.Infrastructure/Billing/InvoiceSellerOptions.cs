namespace TransBrain.Infrastructure.Billing;

/// <summary>
/// The deployment's default company data for invoice documents and XRechnung exports,
/// configured under "InvoiceSeller" in appsettings. Since the invoice designer, this is only
/// the bootstrap: a tenant that saved its own <see cref="TransBrain.Domain.Billing.InvoiceDesign"/>
/// renders with the data from the database instead, because every tenant (Mandant) is its own
/// company. Tenants without a saved design keep falling back to these values — an invoice
/// document without a proper sender is not a usable commercial document.
/// </summary>
public sealed class InvoiceSellerOptions
{
    public const string SectionName = "InvoiceSeller";

    public string Name { get; set; } = string.Empty;

    public string Street { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2, e.g. "DE".</summary>
    public string CountryCode { get; set; } = "DE";

    /// <summary>Umsatzsteuer-Identifikationsnummer, e.g. "DE123456789".</summary>
    public string VatId { get; set; } = string.Empty;

    /// <summary>National Steuernummer; printed when present.</summary>
    public string? TaxNumber { get; set; }

    /// <summary>Billing contact mailbox; also the seller's electronic address in XRechnung.</summary>
    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string Iban { get; set; } = string.Empty;

    public string? Bic { get; set; }

    public string? BankName { get; set; }
}
