using TransBrain.Application.Features.InvoiceDesigns;
using TransBrain.Domain.Billing;

namespace TransBrain.Application.Tests.Features.InvoiceDesigns;

/// <summary>Valid designer inputs the invoice-design tests mutate per case.</summary>
internal static class InvoiceDesignFixture
{
    public static readonly DateTimeOffset Now = new(2027, 3, 10, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A 1x1 transparent PNG — the smallest honest logo a test can upload.</summary>
    public const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    public static InvoiceDesign ADesign(string companyName = "Fallback Logistik GmbH") =>
        InvoiceDesign.Create(
            companyName,
            "Standardstraße 1",
            "80331",
            "München",
            "DE",
            "DE111111111",
            null,
            "billing@fallback.example",
            null,
            "DE02120300000000202051",
            null,
            null,
            InvoiceDesign.DefaultAccentColor,
            InvoiceDesign.DefaultFontSizePoints,
            InvoiceLogoPosition.Right,
            logo: null,
            logoContentType: null,
            headerText: null,
            footerText: null,
            Now).Value;

    public static InvoiceDesignPayload APayload() => new(
        CompanyName: "Nordlicht Transporte GmbH",
        Street: "Hafenstraße 9",
        PostalCode: "20457",
        City: "Hamburg",
        CountryCode: "DE",
        VatId: "DE222222222",
        TaxNumber: "22/222/22222",
        Email: "rechnung@nordlicht.example",
        Phone: "+49 40 555 123",
        Iban: "DE89370400440532013000",
        Bic: "COBADEFFXXX",
        BankName: "Commerzbank",
        AccentColor: "#0B5394",
        FontSizePoints: 10,
        LogoPosition: "Right",
        LogoBase64: null,
        LogoContentType: null,
        HeaderText: "Wir erlauben uns, folgende Leistungen zu berechnen:",
        FooterText: "Vielen Dank für Ihren Auftrag!");
}
