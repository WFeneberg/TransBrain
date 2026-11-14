using AwesomeAssertions;
using FluentValidation.Results;
using TransBrain.Application.Features.InvoiceDesigns;
using TransBrain.Application.Features.InvoiceDesigns.UpdateInvoiceDesign;
using TransBrain.Domain.Billing;

namespace TransBrain.Application.Tests.Features.InvoiceDesigns;

public class UpdateInvoiceDesignCommandValidatorTests
{
    private readonly UpdateInvoiceDesignCommandValidator _validator = new();

    private static UpdateInvoiceDesignCommand ValidCommand => new(InvoiceDesignFixture.APayload());

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        ValidationResult result = _validator.Validate(ValidCommand);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NullDesign_FailsWithoutThrowing()
    {
        ValidationResult result = _validator.Validate(new UpdateInvoiceDesignCommand(null!));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(nameof(InvoiceDesignPayload.CompanyName))]
    [InlineData(nameof(InvoiceDesignPayload.Street))]
    [InlineData(nameof(InvoiceDesignPayload.PostalCode))]
    [InlineData(nameof(InvoiceDesignPayload.City))]
    [InlineData(nameof(InvoiceDesignPayload.VatId))]
    [InlineData(nameof(InvoiceDesignPayload.Iban))]
    public void Validate_RequiredFieldEmpty_Fails(string field)
    {
        InvoiceDesignPayload payload = field switch
        {
            nameof(InvoiceDesignPayload.CompanyName) => InvoiceDesignFixture.APayload() with { CompanyName = "" },
            nameof(InvoiceDesignPayload.Street) => InvoiceDesignFixture.APayload() with { Street = "" },
            nameof(InvoiceDesignPayload.PostalCode) => InvoiceDesignFixture.APayload() with { PostalCode = "" },
            nameof(InvoiceDesignPayload.City) => InvoiceDesignFixture.APayload() with { City = "" },
            nameof(InvoiceDesignPayload.VatId) => InvoiceDesignFixture.APayload() with { VatId = "" },
            _ => InvoiceDesignFixture.APayload() with { Iban = "" },
        };

        ValidationResult result = _validator.Validate(new UpdateInvoiceDesignCommand(payload));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.EndsWith(field));
    }

    [Theory]
    [InlineData("D")]
    [InlineData("DEU")]
    [InlineData("12")]
    public void Validate_InvalidCountryCode_Fails(string countryCode)
    {
        ValidationResult result = _validator.Validate(
            new UpdateInvoiceDesignCommand(InvoiceDesignFixture.APayload() with { CountryCode = countryCode }));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_InvalidEmail_Fails()
    {
        ValidationResult result = _validator.Validate(
            new UpdateInvoiceDesignCommand(InvoiceDesignFixture.APayload() with { Email = "keine-mail" }));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("1F3864")]
    [InlineData("#1F386")]
    [InlineData("#GGGGGG")]
    public void Validate_InvalidAccentColor_Fails(string accentColor)
    {
        ValidationResult result = _validator.Validate(
            new UpdateInvoiceDesignCommand(InvoiceDesignFixture.APayload() with { AccentColor = accentColor }));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(InvoiceDesign.MinFontSizePoints - 1)]
    [InlineData(InvoiceDesign.MaxFontSizePoints + 1)]
    public void Validate_FontSizeOutOfRange_Fails(int fontSizePoints)
    {
        ValidationResult result = _validator.Validate(
            new UpdateInvoiceDesignCommand(InvoiceDesignFixture.APayload() with { FontSizePoints = fontSizePoints }));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_UnknownLogoPosition_Fails()
    {
        ValidationResult result = _validator.Validate(
            new UpdateInvoiceDesignCommand(InvoiceDesignFixture.APayload() with { LogoPosition = "Center" }));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_LogoNotBase64_Fails()
    {
        InvoiceDesignPayload payload = InvoiceDesignFixture.APayload() with
        {
            LogoBase64 = "@@@",
            LogoContentType = "image/png",
        };

        ValidationResult result = _validator.Validate(new UpdateInvoiceDesignCommand(payload));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_LogoWithUnsupportedContentType_Fails()
    {
        InvoiceDesignPayload payload = InvoiceDesignFixture.APayload() with
        {
            LogoBase64 = InvoiceDesignFixture.TinyPngBase64,
            LogoContentType = "image/gif",
        };

        ValidationResult result = _validator.Validate(new UpdateInvoiceDesignCommand(payload));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ValidLogo_Passes()
    {
        InvoiceDesignPayload payload = InvoiceDesignFixture.APayload() with
        {
            LogoBase64 = InvoiceDesignFixture.TinyPngBase64,
            LogoContentType = "image/png",
        };

        ValidationResult result = _validator.Validate(new UpdateInvoiceDesignCommand(payload));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_HeaderTextTooLong_Fails()
    {
        ValidationResult result = _validator.Validate(new UpdateInvoiceDesignCommand(
            InvoiceDesignFixture.APayload() with { HeaderText = new string('x', InvoiceDesign.MaxFreeTextLength + 1) }));

        result.IsValid.Should().BeFalse();
    }
}
