using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.InvoiceDesigns;

/// <summary>
/// The writable shape of a tenant's invoice design, shared by the update command and the
/// designer's preview: what can be saved is exactly what can be previewed. The logo travels
/// base64-encoded because the designer edits it inline with the rest of the form.
/// </summary>
public sealed record InvoiceDesignPayload(
    string? CompanyName,
    string? Street,
    string? PostalCode,
    string? City,
    string? CountryCode,
    string? VatId,
    string? TaxNumber,
    string? Email,
    string? Phone,
    string? Iban,
    string? Bic,
    string? BankName,
    string? AccentColor,
    int FontSizePoints,
    string? LogoPosition,
    string? LogoBase64,
    string? LogoContentType,
    string? HeaderText,
    string? FooterText);

internal static class InvoiceDesignWriteMapping
{
    public static Result<InvoiceDesign> ToNewDesign(this InvoiceDesignPayload payload, DateTimeOffset now)
    {
        Result<(byte[]? Logo, InvoiceLogoPosition Position)> parsed = Parse(payload);
        if (!parsed.IsSuccess)
        {
            return parsed.Error!;
        }

        return InvoiceDesign.Create(
            payload.CompanyName,
            payload.Street,
            payload.PostalCode,
            payload.City,
            payload.CountryCode,
            payload.VatId,
            payload.TaxNumber,
            payload.Email,
            payload.Phone,
            payload.Iban,
            payload.Bic,
            payload.BankName,
            payload.AccentColor,
            payload.FontSizePoints,
            parsed.Value.Position,
            parsed.Value.Logo,
            payload.LogoContentType,
            payload.HeaderText,
            payload.FooterText,
            now);
    }

    public static Result<Unit> ApplyTo(this InvoiceDesignPayload payload, InvoiceDesign design, DateTimeOffset now)
    {
        Result<(byte[]? Logo, InvoiceLogoPosition Position)> parsed = Parse(payload);
        if (!parsed.IsSuccess)
        {
            return parsed.Error!;
        }

        return design.Update(
            payload.CompanyName,
            payload.Street,
            payload.PostalCode,
            payload.City,
            payload.CountryCode,
            payload.VatId,
            payload.TaxNumber,
            payload.Email,
            payload.Phone,
            payload.Iban,
            payload.Bic,
            payload.BankName,
            payload.AccentColor,
            payload.FontSizePoints,
            parsed.Value.Position,
            parsed.Value.Logo,
            payload.LogoContentType,
            payload.HeaderText,
            payload.FooterText,
            now);
    }

    private static Result<(byte[]? Logo, InvoiceLogoPosition Position)> Parse(InvoiceDesignPayload payload)
    {
        byte[]? logo = null;
        if (!string.IsNullOrWhiteSpace(payload.LogoBase64))
        {
            try
            {
                logo = Convert.FromBase64String(payload.LogoBase64);
            }
            catch (FormatException)
            {
                return Error.Validation("InvoiceDesign.LogoNotBase64", "The logo is not valid base64 data.");
            }
        }

        if (!Enum.TryParse(payload.LogoPosition, ignoreCase: true, out InvoiceLogoPosition position))
        {
            return Error.Validation(
                "InvoiceDesign.LogoPositionInvalid",
                $"Logo position must be one of: {string.Join(", ", Enum.GetNames<InvoiceLogoPosition>())}.");
        }

        return (logo, position);
    }
}
