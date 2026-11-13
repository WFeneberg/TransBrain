using AwesomeAssertions;
using TransBrain.Application.Features.Drivers;
using TransBrain.Application.Features.Drivers.UpdateDriver;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Vehicles;

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
        UpdateDriverCommandHandler handler = new(
            repository, InMemoryVehicleCatalogRepository.WithStandardEntries(), cache);

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
        UpdateDriverCommandHandler handler = new(
            repository, InMemoryVehicleCatalogRepository.WithStandardEntries(), cache);

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
        UpdateDriverCommandHandler handler = new(
            repository, InMemoryVehicleCatalogRepository.WithStandardEntries(), cache);

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
        UpdateDriverCommandHandler handler = new(
            repository, InMemoryVehicleCatalogRepository.WithStandardEntries(), cache);

        Result<DriverResponse> result = await handler.Handle(
            new UpdateDriverCommand(driver.Id, "Franz", "Fahrer", ["Rocket"], new DateOnly(2030, 1, 1), null),
            CancellationToken.None);

        result.Error!.Code.Should().Be("Driver.UnknownLicenseClass");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_InactivePhoneTypeTheDriverAlreadyHas_StaysAccepted()
    {
        InMemoryDriverRepository repository = new();
        Driver driver = Driver.Create(
            "Frank", "Fahrer", [LicenseClass.C], new DateOnly(2028, 1, 1), null,
            phoneNumbers: [DriverPhoneNumber.Create("+43 1 1", "Pager").Value]).Value;
        repository.Seed(driver);
        InMemoryVehicleCatalogRepository catalog = new();
        catalog.Seed(VehicleCatalogKind.PhoneType, "Pager", "Pager", isActive: false);
        UpdateDriverCommandHandler handler = new(repository, catalog, new InMemoryCacheService());

        Result<DriverResponse> result = await handler.Handle(
            new UpdateDriverCommand(
                driver.Id, "Frank", "Fahrer", ["C"], new DateOnly(2028, 1, 1), null,
                PhoneNumbers: [new DriverPhoneNumberData("+43 1 99", "pager")]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PhoneNumbers.Should().ContainSingle().Which.Type.Should().Be("Pager");
    }

    [Fact]
    public async Task Handle_InactivePhoneTypeTheDriverDoesNotHave_IsRejected()
    {
        InMemoryDriverRepository repository = new();
        Driver driver = ExistingDriver();
        repository.Seed(driver);
        InMemoryVehicleCatalogRepository catalog = new();
        catalog.Seed(VehicleCatalogKind.PhoneType, "Pager", "Pager", isActive: false);
        UpdateDriverCommandHandler handler = new(repository, catalog, new InMemoryCacheService());

        Result<DriverResponse> result = await handler.Handle(
            new UpdateDriverCommand(
                driver.Id, "Frank", "Fahrer", ["C"], new DateOnly(2028, 1, 1), null,
                PhoneNumbers: [new DriverPhoneNumberData("+43 1 99", "Pager")]),
            CancellationToken.None);

        result.Error!.Failures.Should().ContainKey("PhoneNumbers[0].Type");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_InactiveDocumentTypeTheDriverAlreadyHas_StaysAccepted()
    {
        InMemoryDriverRepository repository = new();
        Driver driver = Driver.Create(
            "Frank", "Fahrer", [LicenseClass.C], new DateOnly(2028, 1, 1), null,
            specialDocuments: [DriverSpecialDocument.Create("Obsolete").Value]).Value;
        repository.Seed(driver);
        InMemoryVehicleCatalogRepository catalog = new();
        catalog.Seed(VehicleCatalogKind.DriverDocumentType, "Obsolete", "Veraltet", isActive: false);
        UpdateDriverCommandHandler handler = new(repository, catalog, new InMemoryCacheService());

        Result<DriverResponse> result = await handler.Handle(
            new UpdateDriverCommand(
                driver.Id, "Frank", "Fahrer", ["C"], new DateOnly(2028, 1, 1), null,
                SpecialDocuments: [new DriverSpecialDocumentData("obsolete", new DateOnly(2024, 5, 2))]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SpecialDocuments.Should().ContainSingle().Which.Type.Should().Be("Obsolete");
    }

    [Fact]
    public async Task Handle_InactiveDocumentTypeTheDriverDoesNotHave_IsRejected()
    {
        InMemoryDriverRepository repository = new();
        Driver driver = ExistingDriver();
        repository.Seed(driver);
        InMemoryVehicleCatalogRepository catalog = new();
        catalog.Seed(VehicleCatalogKind.DriverDocumentType, "Obsolete", "Veraltet", isActive: false);
        UpdateDriverCommandHandler handler = new(repository, catalog, new InMemoryCacheService());

        Result<DriverResponse> result = await handler.Handle(
            new UpdateDriverCommand(
                driver.Id, "Frank", "Fahrer", ["C"], new DateOnly(2028, 1, 1), null,
                SpecialDocuments: [new DriverSpecialDocumentData("Obsolete")]),
            CancellationToken.None);

        result.Error!.Failures.Should().ContainKey("SpecialDocuments[0].Type");
        repository.SaveChangesCallCount.Should().Be(0);
    }
}
