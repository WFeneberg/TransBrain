using FluentValidation;
using TransBrain.Domain.Billing;

namespace TransBrain.Application.Features.InvoiceDesigns.UpdateInvoiceDesign;

/// <remarks>
/// Mirrors — and never extends — the domain's own checks in InvoiceDesign.Create/Update, so a
/// malformed designer payload reports every broken field at once instead of the first coded
/// error the domain would stop at.
/// </remarks>
public sealed class UpdateInvoiceDesignCommandValidator : AbstractValidator<UpdateInvoiceDesignCommand>
{
    public UpdateInvoiceDesignCommandValidator()
    {
        RuleFor(c => c.Design).NotNull();

        When(c => c.Design is not null, () =>
            RuleFor(c => c.Design).SetValidator(new InvoiceDesignPayloadValidator()));
    }
}

internal sealed class InvoiceDesignPayloadValidator : AbstractValidator<InvoiceDesignPayload>
{
    public InvoiceDesignPayloadValidator()
    {
        RuleFor(p => p.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(p => p.Street).NotEmpty().MaximumLength(200);
        RuleFor(p => p.PostalCode).NotEmpty().MaximumLength(20);
        RuleFor(p => p.City).NotEmpty().MaximumLength(200);
        RuleFor(p => p.CountryCode)
            .NotEmpty()
            .Matches("^[A-Za-z]{2}$")
            .WithMessage("Country must be an ISO 3166-1 alpha-2 code, for example 'DE'.");
        RuleFor(p => p.VatId).NotEmpty().MaximumLength(20);
        RuleFor(p => p.TaxNumber).MaximumLength(30);
        RuleFor(p => p.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(p => p.Phone).MaximumLength(50);
        RuleFor(p => p.Iban).NotEmpty().MaximumLength(34);
        RuleFor(p => p.Bic).MaximumLength(11);
        RuleFor(p => p.BankName).MaximumLength(100);

        RuleFor(p => p.AccentColor)
            .NotEmpty()
            .Matches("^#[0-9A-Fa-f]{6}$")
            .WithMessage("Accent colour must be a hex value like '#1F3864'.");

        RuleFor(p => p.FontSizePoints)
            .InclusiveBetween(InvoiceDesign.MinFontSizePoints, InvoiceDesign.MaxFontSizePoints);

        RuleFor(p => p.LogoPosition)
            .Must(value => Enum.TryParse<InvoiceLogoPosition>(value, ignoreCase: true, out _))
            .WithMessage($"Logo position must be one of: {string.Join(", ", Enum.GetNames<InvoiceLogoPosition>())}.");

        RuleFor(p => p.HeaderText).MaximumLength(InvoiceDesign.MaxFreeTextLength);
        RuleFor(p => p.FooterText).MaximumLength(InvoiceDesign.MaxFreeTextLength);

        When(p => !string.IsNullOrWhiteSpace(p.LogoBase64), () =>
        {
            RuleFor(p => p.LogoBase64)
                .Must(BeValidBase64)
                .WithMessage("The logo is not valid base64 data.")
                .Must(value => DecodedLength(value) <= InvoiceDesign.MaxLogoBytes)
                .WithMessage($"The logo must not exceed {InvoiceDesign.MaxLogoBytes / 1024} KB.");

            RuleFor(p => p.LogoContentType)
                .Must(value => value is not null && InvoiceDesign.AllowedLogoContentTypes.Contains(value))
                .WithMessage("The logo must be a PNG or JPEG image.");
        });
    }

    private static bool BeValidBase64(string? value)
    {
        if (value is null)
        {
            return false;
        }

        Span<byte> buffer = new byte[EstimatedDecodedLength(value)];
        return Convert.TryFromBase64String(value, buffer, out _);
    }

    // Upper bound without decoding twice: enough for the size rule, exact length not needed.
    private static int EstimatedDecodedLength(string value) => value.Length / 4 * 3 + 3;

    private static long DecodedLength(string? value)
        => value is null ? 0 : (long)value.Length / 4 * 3;
}
