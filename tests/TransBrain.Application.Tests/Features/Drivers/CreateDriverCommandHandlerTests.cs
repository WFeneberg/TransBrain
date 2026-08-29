using AwesomeAssertions;
using TransBrain.Application.Features.Drivers;
using TransBrain.Application.Features.Drivers.CreateDriver;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Drivers;

public class CreateDriverCommandHandlerTests
{
    private static CreateDriverCommand ValidCommand => new(
        "Frank", "Fahrer", ["C", "CE"], new DateOnly(2028, 6, 30), null);

    [Fact]
    public async Task Handle_ValidCommand_PersistsDriverAndReturnsResponse()
    {
        InMemoryDriverRepository repository = new();
        InMemoryCacheService cache = new();
        CreateDriverCommandHandler handler = new(repository, cache);

        Result<DriverResponse> result = await handler.Handle(ValidCommand, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FirstName.Should().Be("Frank");
        result.Value.LicenseClasses.Should().BeEquivalentTo(["C", "CE"]);
        result.Value.Status.Should().Be("Available");
        repository.Drivers.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_UnknownLicenseClass_ReturnsValidationError()
    {
        InMemoryDriverRepository repository = new();
        InMemoryCacheService cache = new();
        CreateDriverCommandHandler handler = new(repository, cache);

        Result<DriverResponse> result = await handler.Handle(
            ValidCommand with { LicenseClasses = ["C", "Rocket"] }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.UnknownLicenseClass");
        repository.Drivers.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NumericLicenseClass_ReturnsValidationError()
    {
        InMemoryDriverRepository repository = new();
        InMemoryCacheService cache = new();
        CreateDriverCommandHandler handler = new(repository, cache);

        Result<DriverResponse> result = await handler.Handle(
            ValidCommand with { LicenseClasses = ["99"] }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.UnknownLicenseClass");
    }

    [Fact]
    public async Task Handle_BlankFirstName_ReturnsDomainValidationError()
    {
        InMemoryDriverRepository repository = new();
        InMemoryCacheService cache = new();
        CreateDriverCommandHandler handler = new(repository, cache);

        Result<DriverResponse> result = await handler.Handle(
            ValidCommand with { FirstName = "   " }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.FirstNameRequired");
    }

    [Fact]
    public async Task Handle_NoLicenseClasses_ReturnsDomainValidationError()
    {
        InMemoryDriverRepository repository = new();
        InMemoryCacheService cache = new();
        CreateDriverCommandHandler handler = new(repository, cache);

        Result<DriverResponse> result = await handler.Handle(
            ValidCommand with { LicenseClasses = [] }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.LicenseClassRequired");
    }
}
