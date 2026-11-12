namespace TransBrain.Infrastructure.Billing;

/// <summary>
/// The issuing haulier's own company data as printed on invoice documents and embedded in
/// XRechnung exports. Configured under "InvoiceSeller" in appsettings; an invoice document
/// without a proper sender is not a usable commercial document.
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
