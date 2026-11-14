using AwesomeAssertions;
using TransBrain.Application.Features.InvoiceDesigns;
using TransBrain.Application.Features.InvoiceDesigns.GetInvoiceDesign;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.InvoiceDesigns;

public class GetInvoiceDesignQueryHandlerTests
{
    [Fact]
    public async Task Handle_NoSavedDesign_ReturnsDefaultsNotCustomized()
    {
        InMemoryInvoiceDesignRepository repository = new();
        GetInvoiceDesignQueryHandler handler = new(repository);

        Result<InvoiceDesignResponse> result = await handler.Handle(
            new GetInvoiceDesignQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCustomized.Should().BeFalse();
        result.Value.CompanyName.Should().Be(repository.Default.CompanyName);
        result.Value.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_SavedDesign_ReturnsItCustomized()
    {
        InMemoryInvoiceDesignRepository repository = new();
        InvoiceDesign saved = InvoiceDesignFixture.ADesign("Eigene Firma GmbH");
        repository.Seed(saved);
        GetInvoiceDesignQueryHandler handler = new(repository);

        Result<InvoiceDesignResponse> result = await handler.Handle(
            new GetInvoiceDesignQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCustomized.Should().BeTrue();
        result.Value.CompanyName.Should().Be("Eigene Firma GmbH");
        result.Value.UpdatedAt.Should().Be(saved.UpdatedAt);
    }
}
