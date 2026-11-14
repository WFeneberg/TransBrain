using TransBrain.Domain.Billing;

namespace TransBrain.Application.Features.InvoiceDesigns;

public sealed record InvoiceDesignResponse(
    string CompanyName,
    string Street,
    string PostalCode,
    string City,
    string CountryCode,
    string VatId,
    string? TaxNumber,
    string Email,
    string? Phone,
    string Iban,
    string? Bic,
    string? BankName,
    string AccentColor,
    int FontSizePoints,
    string LogoPosition,
    string? LogoBase64,
    string? LogoContentType,
    string? HeaderText,
    string? FooterText,
    bool IsCustomized,
    DateTimeOffset? UpdatedAt)
{
    public static InvoiceDesignResponse From(InvoiceDesign design, bool isCustomized) => new(
        design.CompanyName,
        design.Street,
        design.PostalCode,
        design.City,
        design.CountryCode,
        design.VatId,
        design.TaxNumber,
        design.Email,
        design.Phone,
        design.Iban,
        design.Bic,
        design.BankName,
        design.AccentColor,
        design.FontSizePoints,
        design.LogoPosition.ToString(),
        design.Logo is null ? null : Convert.ToBase64String(design.Logo),
        design.LogoContentType,
        design.HeaderText,
        design.FooterText,
        isCustomized,
        isCustomized ? design.UpdatedAt : null);
}
