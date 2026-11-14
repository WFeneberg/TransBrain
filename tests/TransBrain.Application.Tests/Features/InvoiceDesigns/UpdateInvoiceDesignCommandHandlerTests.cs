using AwesomeAssertions;
using TransBrain.Application.Features.InvoiceDesigns;
using TransBrain.Application.Features.InvoiceDesigns.UpdateInvoiceDesign;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.InvoiceDesigns;

public class UpdateInvoiceDesignCommandHandlerTests
{
    private static (UpdateInvoiceDesignCommandHandler Handler, InMemoryInvoiceDesignRepository Repository) CreateHandler()
    {
        InMemoryInvoiceDesignRepository repository = new();
        FixedTimeProvider clock = new(InvoiceDesignFixture.Now);
        return (new UpdateInvoiceDesignCommandHandler(repository, clock), repository);
    }

    [Fact]
    public async Task Handle_FirstSave_CreatesRow()
    {
        (UpdateInvoiceDesignCommandHandler handler, InMemoryInvoiceDesignRepository repository) = CreateHandler();

        Result<InvoiceDesignResponse> result = await handler.Handle(
            new UpdateInvoiceDesignCommand(InvoiceDesignFixture.APayload()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCustomized.Should().BeTrue();
        result.Value.CompanyName.Should().Be("Nordlicht Transporte GmbH");
        result.Value.UpdatedAt.Should().Be(InvoiceDesignFixture.Now);
        repository.Saved.Should().NotBeNull();
        repository.SaveChangesCalls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SecondSave_UpdatesExistingRow()
    {
        (UpdateInvoiceDesignCommandHandler handler, InMemoryInvoiceDesignRepository repository) = CreateHandler();
        InvoiceDesign existing = InvoiceDesignFixture.ADesign();
        repository.Seed(existing);
        Guid existingId = existing.Id;

        Result<InvoiceDesignResponse> result = await handler.Handle(
            new UpdateInvoiceDesignCommand(InvoiceDesignFixture.APayload()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Saved.Should().BeSameAs(existing);
        repository.Saved!.Id.Should().Be(existingId);
        repository.Saved.CompanyName.Should().Be("Nordlicht Transporte GmbH");
        repository.SaveChangesCalls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithLogo_StoresDecodedLogo()
    {
        (UpdateInvoiceDesignCommandHandler handler, InMemoryInvoiceDesignRepository repository) = CreateHandler();
        InvoiceDesignPayload payload = InvoiceDesignFixture.APayload() with
        {
            LogoBase64 = InvoiceDesignFixture.TinyPngBase64,
            LogoContentType = "image/png",
        };

        Result<InvoiceDesignResponse> result = await handler.Handle(
            new UpdateInvoiceDesignCommand(payload), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.LogoBase64.Should().Be(InvoiceDesignFixture.TinyPngBase64);
        result.Value.LogoContentType.Should().Be("image/png");
        repository.Saved!.Logo.Should().BeEquivalentTo(
            Convert.FromBase64String(InvoiceDesignFixture.TinyPngBase64));
    }

    [Fact]
    public async Task Handle_InvalidBase64Logo_ReturnsValidationError()
    {
        (UpdateInvoiceDesignCommandHandler handler, InMemoryInvoiceDesignRepository repository) = CreateHandler();
        InvoiceDesignPayload payload = InvoiceDesignFixture.APayload() with
        {
            LogoBase64 = "@@not-base64@@",
            LogoContentType = "image/png",
        };

        Result<InvoiceDesignResponse> result = await handler.Handle(
            new UpdateInvoiceDesignCommand(payload), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("InvoiceDesign.LogoNotBase64");
        repository.Saved.Should().BeNull();
        repository.SaveChangesCalls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_InvalidLogoPosition_ReturnsValidationError()
    {
        (UpdateInvoiceDesignCommandHandler handler, _) = CreateHandler();
        InvoiceDesignPayload payload = InvoiceDesignFixture.APayload() with { LogoPosition = "Middle" };

        Result<InvoiceDesignResponse> result = await handler.Handle(
            new UpdateInvoiceDesignCommand(payload), CancellationToken.None);

        result.Error!.Code.Should().Be("InvoiceDesign.LogoPositionInvalid");
    }

    [Fact]
    public async Task Handle_DomainRejectsPayload_DoesNotSave()
    {
        (UpdateInvoiceDesignCommandHandler handler, InMemoryInvoiceDesignRepository repository) = CreateHandler();
        InvoiceDesignPayload payload = InvoiceDesignFixture.APayload() with { CompanyName = " " };

        Result<InvoiceDesignResponse> result = await handler.Handle(
            new UpdateInvoiceDesignCommand(payload), CancellationToken.None);

        result.Error!.Code.Should().Be("InvoiceDesign.CompanyNameRequired");
        repository.SaveChangesCalls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_DomainRejectsUpdate_ExistingRowKeepsValues()
    {
        (UpdateInvoiceDesignCommandHandler handler, InMemoryInvoiceDesignRepository repository) = CreateHandler();
        repository.Seed(InvoiceDesignFixture.ADesign("Fallback Logistik GmbH"));
        InvoiceDesignPayload payload = InvoiceDesignFixture.APayload() with { AccentColor = "rot" };

        Result<InvoiceDesignResponse> result = await handler.Handle(
            new UpdateInvoiceDesignCommand(payload), CancellationToken.None);

        result.Error!.Code.Should().Be("InvoiceDesign.AccentColorInvalid");
        repository.Saved!.CompanyName.Should().Be("Fallback Logistik GmbH");
        repository.SaveChangesCalls.Should().Be(0);
    }
}
