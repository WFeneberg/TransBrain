using AwesomeAssertions;
using TransBrain.Application.Features.Drivers;
using TransBrain.Application.Features.Drivers.CreateDriver;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

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
        CreateDriverCommandHandler handler = new(
            repository, InMemoryVehicleCatalogRepository.WithStandardEntries(), cache);

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
        CreateDriverCommandHandler handler = new(
            repository, InMemoryVehicleCatalogRepository.WithStandardEntries(), cache);

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
        CreateDriverCommandHandler handler = new(
            repository, InMemoryVehicleCatalogRepository.WithStandardEntries(), cache);

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
        CreateDriverCommandHandler handler = new(
            repository, InMemoryVehicleCatalogRepository.WithStandardEntries(), cache);

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
        CreateDriverCommandHandler handler = new(
            repository, InMemoryVehicleCatalogRepository.WithStandardEntries(), cache);

        Result<DriverResponse> result = await handler.Handle(
            ValidCommand with { LicenseClasses = [] }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.LicenseClassRequired");
    }

    [Fact]
    public async Task Handle_PhoneTypeInAnyCasing_StoresCanonicalCatalogCode()
    {
        InMemoryDriverRepository repository = new();
        CreateDriverCommandHandler handler = new(
            repository, InMemoryVehicleCatalogRepository.WithStandardEntries(), new InMemoryCacheService());

        Result<DriverResponse> result = await handler.Handle(
            ValidCommand with { PhoneNumbers = [new DriverPhoneNumberData("+43 1 234", "private")] },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.PhoneNumbers.Should().ContainSingle().Which.Type.Should().Be("Private");
    }

    [Fact]
    public async Task Handle_UnknownPhoneType_ReturnsValidationFailureKeyedByRow()
    {
        InMemoryDriverRepository repository = new();
        CreateDriverCommandHandler handler = new(
            repository, InMemoryVehicleCatalogRepository.WithStandardEntries(), new InMemoryCacheService());

        Result<DriverResponse> result = await handler.Handle(
            ValidCommand with
            {
                PhoneNumbers =
                [
                    new DriverPhoneNumberData("+43 1 1", "Private"),
                    new DriverPhoneNumberData("+43 1 2", "Mobil"),
                ],
            },
            CancellationToken.None);

        result.Error!.Failures!["PhoneNumbers[1].Type"].Should().ContainSingle()
            .Which.Should().Contain("phone type");
        repository.Drivers.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_InactivePhoneType_IsRejectedOnCreate()
    {
        InMemoryVehicleCatalogRepository catalog = new();
        catalog.Seed(VehicleCatalogKind.PhoneType, "Pager", "Pager", isActive: false);
        CreateDriverCommandHandler handler = new(
            new InMemoryDriverRepository(), catalog, new InMemoryCacheService());

        Result<DriverResponse> result = await handler.Handle(
            ValidCommand with { PhoneNumbers = [new DriverPhoneNumberData("+43 1 1", "Pager")] },
            CancellationToken.None);

        result.Error!.Failures.Should().ContainKey("PhoneNumbers[0].Type");
    }

    [Fact]
    public async Task Handle_DocumentTypeInAnyCasing_StoresCanonicalCatalogCode()
    {
        InMemoryDriverRepository repository = new();
        CreateDriverCommandHandler handler = new(
            repository, InMemoryVehicleCatalogRepository.WithStandardEntries(), new InMemoryCacheService());

        Result<DriverResponse> result = await handler.Handle(
            ValidCommand with { SpecialDocuments = [new DriverSpecialDocumentData("tuvassessment")] },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.SpecialDocuments.Should().ContainSingle().Which.Type.Should().Be("TuvAssessment");
    }

    [Fact]
    public async Task Handle_UnknownDocumentType_ReturnsValidationFailureKeyedByRow()
    {
        InMemoryDriverRepository repository = new();
        CreateDriverCommandHandler handler = new(
            repository, InMemoryVehicleCatalogRepository.WithStandardEntries(), new InMemoryCacheService());

        Result<DriverResponse> result = await handler.Handle(
            ValidCommand with
            {
                SpecialDocuments =
                [
                    new DriverSpecialDocumentData("DangerousGoods"),
                    new DriverSpecialDocumentData("ADR-Schein"),
                ],
            },
            CancellationToken.None);

        result.Error!.Failures!["SpecialDocuments[1].Type"].Should().ContainSingle()
            .Which.Should().Contain("driver document type");
        repository.Drivers.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_InactiveDocumentType_IsRejectedOnCreate()
    {
        InMemoryVehicleCatalogRepository catalog = new();
        catalog.Seed(VehicleCatalogKind.DriverDocumentType, "Obsolete", "Veraltet", isActive: false);
        CreateDriverCommandHandler handler = new(
            new InMemoryDriverRepository(), catalog, new InMemoryCacheService());

        Result<DriverResponse> result = await handler.Handle(
            ValidCommand with { SpecialDocuments = [new DriverSpecialDocumentData("Obsolete")] },
            CancellationToken.None);

        result.Error!.Failures.Should().ContainKey("SpecialDocuments[0].Type");
    }
}
