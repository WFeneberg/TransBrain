using AwesomeAssertions;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;

namespace TransBrain.Domain.Tests.Billing;

public class InvoiceDesignTests
{
    private static readonly DateTimeOffset Now = new(2027, 3, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly byte[] TinyLogo = [0x89, 0x50, 0x4E, 0x47];

    private static Result<InvoiceDesign> Create(
        string companyName = "Nordlicht Transporte GmbH",
        string street = "Hafenstraße 9",
        string postalCode = "20457",
        string city = "Hamburg",
        string countryCode = "DE",
        string vatId = "DE222222222",
        string? taxNumber = "22/222/22222",
        string email = "rechnung@nordlicht.example",
        string? phone = "+49 40 555 123",
        string iban = "DE89370400440532013000",
        string? bic = "COBADEFFXXX",
        string? bankName = "Commerzbank",
        string accentColor = "#0B5394",
        int fontSizePoints = 10,
        InvoiceLogoPosition logoPosition = InvoiceLogoPosition.Right,
        byte[]? logo = null,
        string? logoContentType = null,
        string? headerText = "Kopfzeile",
        string? footerText = "Fußzeile") =>
        InvoiceDesign.Create(
            companyName, street, postalCode, city, countryCode, vatId, taxNumber, email, phone,
            iban, bic, bankName, accentColor, fontSizePoints, logoPosition, logo, logoContentType,
            headerText, footerText, Now);

    [Fact]
    public void Create_ValidInput_NormalizesAndSucceeds()
    {
        Result<InvoiceDesign> result = Create(countryCode: " de ", accentColor: "#0b5394");

        result.IsSuccess.Should().BeTrue();
        InvoiceDesign design = result.Value;
        design.CompanyName.Should().Be("Nordlicht Transporte GmbH");
        design.CountryCode.Should().Be("DE");
        design.AccentColor.Should().Be("#0B5394");
        design.FontSizePoints.Should().Be(10);
        design.LogoPosition.Should().Be(InvoiceLogoPosition.Right);
        design.UpdatedAt.Should().Be(Now);
        design.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_OptionalBlanks_BecomeNull()
    {
        Result<InvoiceDesign> result = Create(
            taxNumber: " ", phone: "", bic: "  ", bankName: null, headerText: " ", footerText: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.TaxNumber.Should().BeNull();
        result.Value.Phone.Should().BeNull();
        result.Value.Bic.Should().BeNull();
        result.Value.BankName.Should().BeNull();
        result.Value.HeaderText.Should().BeNull();
        result.Value.FooterText.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingCompanyName_Fails(string companyName)
    {
        Result<InvoiceDesign> result = Create(companyName: companyName);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("InvoiceDesign.CompanyNameRequired");
    }

    [Fact]
    public void Create_MissingStreet_Fails()
    {
        Create(street: " ").Error!.Code.Should().Be("InvoiceDesign.StreetRequired");
    }

    [Fact]
    public void Create_MissingPostalCode_Fails()
    {
        Create(postalCode: "").Error!.Code.Should().Be("InvoiceDesign.PostalCodeRequired");
    }

    [Fact]
    public void Create_MissingCity_Fails()
    {
        Create(city: "").Error!.Code.Should().Be("InvoiceDesign.CityRequired");
    }

    [Theory]
    [InlineData("D")]
    [InlineData("DEU")]
    [InlineData("12")]
    [InlineData("")]
    public void Create_InvalidCountryCode_Fails(string countryCode)
    {
        Create(countryCode: countryCode).Error!.Code.Should().Be("InvoiceDesign.CountryInvalid");
    }

    [Fact]
    public void Create_MissingVatId_Fails()
    {
        Create(vatId: " ").Error!.Code.Should().Be("InvoiceDesign.VatIdRequired");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Create_InvalidEmail_Fails(string email)
    {
        Create(email: email).Error!.Code.Should().Be("InvoiceDesign.EmailInvalid");
    }

    [Fact]
    public void Create_MissingIban_Fails()
    {
        Create(iban: "").Error!.Code.Should().Be("InvoiceDesign.IbanRequired");
    }

    [Theory]
    [InlineData("1F3864")]
    [InlineData("#1F386")]
    [InlineData("#1F38642")]
    [InlineData("#GGGGGG")]
    [InlineData("")]
    public void Create_InvalidAccentColor_Fails(string accentColor)
    {
        Create(accentColor: accentColor).Error!.Code.Should().Be("InvoiceDesign.AccentColorInvalid");
    }

    [Theory]
    [InlineData(InvoiceDesign.MinFontSizePoints - 1)]
    [InlineData(InvoiceDesign.MaxFontSizePoints + 1)]
    public void Create_FontSizeOutOfRange_Fails(int fontSizePoints)
    {
        Create(fontSizePoints: fontSizePoints).Error!.Code.Should().Be("InvoiceDesign.FontSizeOutOfRange");
    }

    [Theory]
    [InlineData(InvoiceDesign.MinFontSizePoints)]
    [InlineData(InvoiceDesign.MaxFontSizePoints)]
    public void Create_FontSizeAtBoundary_Succeeds(int fontSizePoints)
    {
        Create(fontSizePoints: fontSizePoints).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_LogoWithoutContentType_Fails()
    {
        Result<InvoiceDesign> result = Create(logo: TinyLogo, logoContentType: null);

        result.Error!.Code.Should().Be("InvoiceDesign.LogoContentTypeInvalid");
    }

    [Fact]
    public void Create_LogoWithUnsupportedContentType_Fails()
    {
        Result<InvoiceDesign> result = Create(logo: TinyLogo, logoContentType: "image/gif");

        result.Error!.Code.Should().Be("InvoiceDesign.LogoContentTypeInvalid");
    }

    [Fact]
    public void Create_LogoTooLarge_Fails()
    {
        byte[] oversized = new byte[InvoiceDesign.MaxLogoBytes + 1];

        Result<InvoiceDesign> result = Create(logo: oversized, logoContentType: "image/png");

        result.Error!.Code.Should().Be("InvoiceDesign.LogoTooLarge");
    }

    [Fact]
    public void Create_ValidPngLogo_StoresLogoAndContentType()
    {
        Result<InvoiceDesign> result = Create(logo: TinyLogo, logoContentType: "image/png");

        result.IsSuccess.Should().BeTrue();
        result.Value.Logo.Should().BeEquivalentTo(TinyLogo);
        result.Value.LogoContentType.Should().Be("image/png");
    }

    [Fact]
    public void Create_ContentTypeWithoutLogo_IsDropped()
    {
        Result<InvoiceDesign> result = Create(logo: null, logoContentType: "image/png");

        result.IsSuccess.Should().BeTrue();
        result.Value.Logo.Should().BeNull();
        result.Value.LogoContentType.Should().BeNull();
    }

    [Fact]
    public void Create_HeaderTextTooLong_Fails()
    {
        Create(headerText: new string('x', 1001)).Error!.Code.Should().Be("InvoiceDesign.HeaderTextTooLong");
    }

    [Fact]
    public void Create_FooterTextTooLong_Fails()
    {
        Create(footerText: new string('x', 1001)).Error!.Code.Should().Be("InvoiceDesign.FooterTextTooLong");
    }

    [Fact]
    public void Update_ValidInput_OverwritesAllFieldsAndTimestamp()
    {
        InvoiceDesign design = Create().Value;
        DateTimeOffset later = Now.AddDays(1);

        Result<Unit> result = design.Update(
            "Südwind Spedition AG", "Bergweg 2", "70173", "Stuttgart", "AT", "ATU99999999",
            null, "office@suedwind.example", null, "AT611904300234573201", null, null,
            "#B45309", 12, InvoiceLogoPosition.Left, TinyLogo, "image/jpeg",
            null, "Danke!", later);

        result.IsSuccess.Should().BeTrue();
        design.CompanyName.Should().Be("Südwind Spedition AG");
        design.CountryCode.Should().Be("AT");
        design.AccentColor.Should().Be("#B45309");
        design.FontSizePoints.Should().Be(12);
        design.LogoPosition.Should().Be(InvoiceLogoPosition.Left);
        design.Logo.Should().BeEquivalentTo(TinyLogo);
        design.LogoContentType.Should().Be("image/jpeg");
        design.HeaderText.Should().BeNull();
        design.FooterText.Should().Be("Danke!");
        design.UpdatedAt.Should().Be(later);
    }

    [Fact]
    public void Update_InvalidInput_LeavesDesignUnchanged()
    {
        InvoiceDesign design = Create().Value;

        Result<Unit> result = design.Update(
            "", "Bergweg 2", "70173", "Stuttgart", "DE", "DE999999999",
            null, "office@x.example", null, "DE89370400440532013000", null, null,
            InvoiceDesign.DefaultAccentColor, 10, InvoiceLogoPosition.Left, null, null,
            null, null, Now.AddDays(1));

        result.IsSuccess.Should().BeFalse();
        design.CompanyName.Should().Be("Nordlicht Transporte GmbH");
        design.UpdatedAt.Should().Be(Now);
    }
}
