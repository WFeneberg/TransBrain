using TransBrain.Domain.Common;

namespace TransBrain.Domain.Billing;

/// <summary>
/// A tenant's invoice stationery: the issuing company's own data (each tenant is a distinct
/// legal entity with its own letterhead, VAT id and bank account) plus the layout choices the
/// invoice designer offers — logo, accent colour, base font size and free header/footer texts.
/// One row per tenant; invoices and XRechnung exports render against the saved design, or
/// against the deployment's configured defaults while a tenant has not customised anything yet.
/// </summary>
public sealed class InvoiceDesign
{
    public const string DefaultAccentColor = "#1F3864";

    public const int DefaultFontSizePoints = 10;

    public const int MinFontSizePoints = 8;

    public const int MaxFontSizePoints = 14;

    /// <summary>1 MiB — a letterhead logo, not a photo archive.</summary>
    public const int MaxLogoBytes = 1024 * 1024;

    public const int MaxFreeTextLength = 1000;

    public static readonly IReadOnlyList<string> AllowedLogoContentTypes = ["image/png", "image/jpeg"];

    private const int CountryLength = 2;

    // EF Core materialization only. Every other construction goes through Create.
    private InvoiceDesign()
    {
        CompanyName = null!;
        Street = null!;
        PostalCode = null!;
        City = null!;
        CountryCode = null!;
        VatId = null!;
        Email = null!;
        Iban = null!;
        AccentColor = null!;
    }

    public Guid Id { get; private set; }

    public string CompanyName { get; private set; }

    public string Street { get; private set; }

    public string PostalCode { get; private set; }

    public string City { get; private set; }

    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    public string CountryCode { get; private set; }

    /// <summary>Umsatzsteuer-Identifikationsnummer; XRechnung refuses a seller without one.</summary>
    public string VatId { get; private set; }

    public string? TaxNumber { get; private set; }

    public string Email { get; private set; }

    public string? Phone { get; private set; }

    public string Iban { get; private set; }

    public string? Bic { get; private set; }

    public string? BankName { get; private set; }

    /// <summary>Hex colour (#RRGGBB) for the letterhead and table accents.</summary>
    public string AccentColor { get; private set; } = DefaultAccentColor;

    public int FontSizePoints { get; private set; } = DefaultFontSizePoints;

    public InvoiceLogoPosition LogoPosition { get; private set; } = InvoiceLogoPosition.Right;

    public byte[]? Logo { get; private set; }

    public string? LogoContentType { get; private set; }

    /// <summary>Free text printed between the invoice title and the line table.</summary>
    public string? HeaderText { get; private set; }

    /// <summary>Free text printed below the payment note, e.g. thanks or legal remarks.</summary>
    public string? FooterText { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<InvoiceDesign> Create(
        string? companyName,
        string? street,
        string? postalCode,
        string? city,
        string? countryCode,
        string? vatId,
        string? taxNumber,
        string? email,
        string? phone,
        string? iban,
        string? bic,
        string? bankName,
        string? accentColor,
        int fontSizePoints,
        InvoiceLogoPosition logoPosition,
        byte[]? logo,
        string? logoContentType,
        string? headerText,
        string? footerText,
        DateTimeOffset updatedAt)
    {
        var design = new InvoiceDesign { Id = Guid.CreateVersion7() };
        Result<Unit> applied = design.Apply(
            companyName, street, postalCode, city, countryCode, vatId, taxNumber, email, phone,
            iban, bic, bankName, accentColor, fontSizePoints, logoPosition, logo, logoContentType,
            headerText, footerText, updatedAt);

        return applied.IsSuccess ? design : applied.Error!;
    }

    public Result<Unit> Update(
        string? companyName,
        string? street,
        string? postalCode,
        string? city,
        string? countryCode,
        string? vatId,
        string? taxNumber,
        string? email,
        string? phone,
        string? iban,
        string? bic,
        string? bankName,
        string? accentColor,
        int fontSizePoints,
        InvoiceLogoPosition logoPosition,
        byte[]? logo,
        string? logoContentType,
        string? headerText,
        string? footerText,
        DateTimeOffset updatedAt)
        => Apply(
            companyName, street, postalCode, city, countryCode, vatId, taxNumber, email, phone,
            iban, bic, bankName, accentColor, fontSizePoints, logoPosition, logo, logoContentType,
            headerText, footerText, updatedAt);

    // Create and Update enforce identical invariants: a design that could be saved invalid once
    // would print an unusable commercial document forever after.
    private Result<Unit> Apply(
        string? companyName,
        string? street,
        string? postalCode,
        string? city,
        string? countryCode,
        string? vatId,
        string? taxNumber,
        string? email,
        string? phone,
        string? iban,
        string? bic,
        string? bankName,
        string? accentColor,
        int fontSizePoints,
        InvoiceLogoPosition logoPosition,
        byte[]? logo,
        string? logoContentType,
        string? headerText,
        string? footerText,
        DateTimeOffset updatedAt)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            return Error.Validation("InvoiceDesign.CompanyNameRequired", "Company name must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(street))
        {
            return Error.Validation("InvoiceDesign.StreetRequired", "Street must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(postalCode))
        {
            return Error.Validation("InvoiceDesign.PostalCodeRequired", "Postal code must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            return Error.Validation("InvoiceDesign.CityRequired", "City must not be empty.");
        }

        // Same shape rule as Address.Create: validate the trimmed input before uppercasing,
        // because case conversion can change a string's length.
        string trimmedCountry = countryCode?.Trim() ?? string.Empty;
        if (trimmedCountry.Length != CountryLength || !trimmedCountry.All(char.IsAsciiLetter))
        {
            return Error.Validation(
                "InvoiceDesign.CountryInvalid",
                "Country must be an ISO 3166-1 alpha-2 code, for example 'DE'.");
        }

        if (string.IsNullOrWhiteSpace(vatId))
        {
            return Error.Validation("InvoiceDesign.VatIdRequired", "VAT id must not be empty.");
        }

        string trimmedEmail = email?.Trim() ?? string.Empty;
        if (trimmedEmail.Length == 0 || !trimmedEmail.Contains('@'))
        {
            return Error.Validation("InvoiceDesign.EmailInvalid", "A billing e-mail address is required.");
        }

        if (string.IsNullOrWhiteSpace(iban))
        {
            return Error.Validation("InvoiceDesign.IbanRequired", "IBAN must not be empty.");
        }

        string trimmedColor = accentColor?.Trim() ?? string.Empty;
        if (!IsHexColor(trimmedColor))
        {
            return Error.Validation(
                "InvoiceDesign.AccentColorInvalid",
                "Accent colour must be a hex value like '#1F3864'.");
        }

        if (fontSizePoints is < MinFontSizePoints or > MaxFontSizePoints)
        {
            return Error.Validation(
                "InvoiceDesign.FontSizeOutOfRange",
                $"Font size must be between {MinFontSizePoints} and {MaxFontSizePoints} points.");
        }

        if (logo is { Length: 0 })
        {
            logo = null;
        }

        if (logo is not null)
        {
            if (logo.Length > MaxLogoBytes)
            {
                return Error.Validation(
                    "InvoiceDesign.LogoTooLarge",
                    $"The logo must not exceed {MaxLogoBytes / 1024} KB.");
            }

            if (logoContentType is null || !AllowedLogoContentTypes.Contains(logoContentType))
            {
                return Error.Validation(
                    "InvoiceDesign.LogoContentTypeInvalid",
                    "The logo must be a PNG or JPEG image.");
            }
        }

        string? normalizedHeader = OptionalText.Normalize(headerText);
        if (normalizedHeader?.Length > MaxFreeTextLength)
        {
            return Error.Validation(
                "InvoiceDesign.HeaderTextTooLong",
                $"The header text must not exceed {MaxFreeTextLength} characters.");
        }

        string? normalizedFooter = OptionalText.Normalize(footerText);
        if (normalizedFooter?.Length > MaxFreeTextLength)
        {
            return Error.Validation(
                "InvoiceDesign.FooterTextTooLong",
                $"The footer text must not exceed {MaxFreeTextLength} characters.");
        }

        CompanyName = companyName.Trim();
        Street = street.Trim();
        PostalCode = postalCode.Trim();
        City = city.Trim();
        CountryCode = trimmedCountry.ToUpperInvariant();
        VatId = vatId.Trim();
        TaxNumber = OptionalText.Normalize(taxNumber);
        Email = trimmedEmail;
        Phone = OptionalText.Normalize(phone);
        Iban = iban.Trim();
        Bic = OptionalText.Normalize(bic);
        BankName = OptionalText.Normalize(bankName);
        AccentColor = trimmedColor.ToUpperInvariant();
        FontSizePoints = fontSizePoints;
        LogoPosition = logoPosition;
        Logo = logo;
        LogoContentType = logo is null ? null : logoContentType;
        HeaderText = normalizedHeader;
        FooterText = normalizedFooter;
        UpdatedAt = updatedAt.ToUniversalTime();

        return Unit.Value;
    }

    private static bool IsHexColor(string value) =>
        value.Length == 7
        && value[0] == '#'
        && value.Skip(1).All(char.IsAsciiHexDigit);
}
