using AwesomeAssertions;
using TransBrain.Application.Features.Drivers;
using TransBrain.Application.Features.Drivers.UpdateDriver;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Tests.Features.Drivers;

public class UpdateDriverCommandHandlerTests
{
    private static Driver ExistingDriver() =>
        Driver.Create("Frank", "Fahrer", [LicenseClass.C], new DateOnly(2028, 1, 1), null).Value;

    [Fact]
    public async Task Handle_KnownDriver_UpdatesFieldsAndSavesOnce()
    {
        InMemoryDriverRepository repository = new();
        Driver driver = ExistingDriver();
        repository.Seed(driver);
        InMemoryCacheService cache = new();
        UpdateDriverCommandHandler handler = new(repository, cache);

        Result<DriverResponse> result = await handler.Handle(
            new UpdateDriverCommand(driver.Id, "Franz", "Fahrer", ["B"], new DateOnly(2030, 1, 1), "sub-1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FirstName.Should().Be("Franz");
        result.Value.LicenseClasses.Should().BeEquivalentTo(["B"]);
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnknownDriver_ReturnsNotFoundAndDoesNotSave()
    {
        InMemoryDriverRepository repository = new();
        InMemoryCacheService cache = new();
        UpdateDriverCommandHandler handler = new(repository, cache);

        Result<DriverResponse> result = await handler.Handle(
            new UpdateDriverCommand(Guid.CreateVersion7(), "A", "B", ["B"], new DateOnly(2030, 1, 1), null),
            CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_InvalidUpdate_LeavesDriverUnchangedAndDoesNotSave()
    {
        InMemoryDriverRepository repository = new();
        Driver driver = ExistingDriver();
        repository.Seed(driver);
        InMemoryCacheService cache = new();
        UpdateDriverCommandHandler handler = new(repository, cache);

        Result<DriverResponse> result = await handler.Handle(
            new UpdateDriverCommand(driver.Id, "   ", "Fahrer", ["B"], new DateOnly(2030, 1, 1), null),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.FirstNameRequired");
        driver.FirstName.Should().Be("Frank");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_UnknownLicenseClass_ReturnsValidationErrorAndDoesNotSave()
    {
        InMemoryDriverRepository repository = new();
        Driver driver = ExistingDriver();
        repository.Seed(driver);
        InMemoryCacheService cache = new();
        UpdateDriverCommandHandler handler = new(repository, cache);

        Result<DriverResponse> result = await handler.Handle(
            new UpdateDriverCommand(driver.Id, "Franz", "Fahrer", ["Rocket"], new DateOnly(2030, 1, 1), null),
            CancellationToken.None);

        result.Error!.Code.Should().Be("Driver.UnknownLicenseClass");
        repository.SaveChangesCallCount.Should().Be(0);
    }
}
