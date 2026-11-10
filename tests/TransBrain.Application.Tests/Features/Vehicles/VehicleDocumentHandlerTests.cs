using AwesomeAssertions;
using FluentValidation.Results;
using TransBrain.Application.Features.Vehicles.VehicleDocuments;
using TransBrain.Application.Features.Vehicles.VehicleDocuments.AddVehicleDocument;
using TransBrain.Application.Features.Vehicles.VehicleDocuments.DeleteVehicleDocument;
using TransBrain.Application.Features.Vehicles.VehicleDocuments.ListVehicleDocuments;
using TransBrain.Application.Features.Vehicles.VehicleDocuments.UpdateVehicleDocument;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Vehicles;

public class VehicleDocumentHandlerTests
{
    private static readonly DateTimeOffset Earlier = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly InMemoryVehicleRepository _repository = new();
    private readonly InMemoryVehicleCatalogRepository _catalog;
    private readonly Vehicle _vehicle;

    public VehicleDocumentHandlerTests()
    {
        _catalog = InMemoryVehicleCatalogRepository.WithStandardEntries(_repository);
        _vehicle = Vehicle.Create(
            LicensePlate.Create("M-AB 1234").Value, VehicleType.Tractor, 24_000, 13.6m, new DateOnly(2027, 3, 31)).Value;
        _repository.Seed(_vehicle);
    }

    private AddVehicleDocumentCommandHandler AddHandler() =>
        new(_repository, _catalog, new StubCurrentUser("admin"), new FixedTimeProvider());

    private UpdateVehicleDocumentCommandHandler UpdateHandler() =>
        new(_repository, _catalog, new StubCurrentUser("admin"), new FixedTimeProvider());

    private VehicleDocument SeedDocument(string type, DateOnly? validUntil = null, Guid? vehicleId = null)
    {
        VehicleDocument document = VehicleDocument.Create(
            vehicleId ?? _vehicle.Id, type, null, validUntil, null, "someone", Earlier).Value;
        _repository.SeedDocuments(document);
        return document;
    }

    [Fact]
    public async Task Add_ValidDocument_PersistsItWithCanonicalTypeAndAuditStamp()
    {
        Result<VehicleDocumentResponse> result = await AddHandler().Handle(
            new AddVehicleDocumentCommand(
                _vehicle.Id, "adr", new DateOnly(2024, 1, 31), new DateOnly(2025, 1, 31), "Erstprüfung"),
            CancellationToken.None);

        result.Value.Should().BeEquivalentTo(new
        {
            VehicleId = _vehicle.Id,
            DocumentType = "ADR",
            LastCheckedOn = new DateOnly(2024, 1, 31),
            ValidUntil = new DateOnly(2025, 1, 31),
            Remark = "Erstprüfung",
            ModifiedAt = FixedTimeProvider.Default,
            ModifiedBy = "admin",
        });
        _repository.Documents.Should().ContainSingle().Which.Id.Should().Be(result.Value.Id);
    }

    [Fact]
    public async Task Add_UnknownVehicle_ReturnsNotFound()
    {
        Result<VehicleDocumentResponse> result = await AddHandler().Handle(
            new AddVehicleDocumentCommand(Guid.CreateVersion7(), "ADR"), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Add_UnknownOrInactiveType_ReturnsFailureKeyedByDocumentType()
    {
        _catalog.Seed(VehicleCatalogKind.DocumentType, "OLD", "Veraltet", isActive: false);

        Result<VehicleDocumentResponse> unknown = await AddHandler().Handle(
            new AddVehicleDocumentCommand(_vehicle.Id, "NOPE"), CancellationToken.None);
        Result<VehicleDocumentResponse> inactive = await AddHandler().Handle(
            new AddVehicleDocumentCommand(_vehicle.Id, "OLD"), CancellationToken.None);

        unknown.Error!.Failures!["DocumentType"].Should().ContainSingle().Which.Should().Contain("document type");
        inactive.Error!.Failures.Should().ContainKey("DocumentType");
        _repository.Documents.Should().BeEmpty();
    }

    [Fact]
    public async Task Add_TypeTheVehicleAlreadyHas_ReturnsConflict()
    {
        SeedDocument("ADR");

        Result<VehicleDocumentResponse> result = await AddHandler().Handle(
            new AddVehicleDocumentCommand(_vehicle.Id, "Adr"), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("VehicleDocument.DuplicateType");
        _repository.Documents.Should().ContainSingle();
    }

    [Fact]
    public async Task Add_SameTypeOnAnotherVehicle_Succeeds()
    {
        SeedDocument("ADR", vehicleId: Guid.CreateVersion7());

        Result<VehicleDocumentResponse> result = await AddHandler().Handle(
            new AddVehicleDocumentCommand(_vehicle.Id, "ADR"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Add_ExpiryBeforeLastCheck_ReturnsDomainError()
    {
        Result<VehicleDocumentResponse> result = await AddHandler().Handle(
            new AddVehicleDocumentCommand(_vehicle.Id, "ADR", new DateOnly(2025, 2, 1), new DateOnly(2025, 1, 1)),
            CancellationToken.None);

        result.Error!.Code.Should().Be("VehicleDocument.ValidUntilBeforeLastChecked");
        _repository.Documents.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_ExistingDocument_ReplacesValuesAndRestamps()
    {
        VehicleDocument document = SeedDocument("ADR", new DateOnly(2025, 1, 31));

        Result<VehicleDocumentResponse> result = await UpdateHandler().Handle(
            new UpdateVehicleDocumentCommand(
                _vehicle.Id, document.Id, "ADR", new DateOnly(2025, 1, 15), new DateOnly(2030, 1, 31), "verlängert"),
            CancellationToken.None);

        result.Value.ValidUntil.Should().Be(new DateOnly(2030, 1, 31));
        result.Value.Remark.Should().Be("verlängert");
        result.Value.ModifiedBy.Should().Be("admin");
        result.Value.ModifiedAt.Should().Be(FixedTimeProvider.Default);
        _repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Update_ToATypeTheVehicleAlreadyHas_ReturnsConflict()
    {
        SeedDocument("ADR");
        VehicleDocument tachograph = SeedDocument("Tachograph");

        Result<VehicleDocumentResponse> result = await UpdateHandler().Handle(
            new UpdateVehicleDocumentCommand(_vehicle.Id, tachograph.Id, "ADR"), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
        tachograph.DocumentType.Should().Be("Tachograph");
    }

    [Fact]
    public async Task Update_KeepingAnInactiveType_IsAllowed()
    {
        _catalog.Seed(VehicleCatalogKind.DocumentType, "OLD", "Veraltet", isActive: false);
        VehicleDocument document = SeedDocument("OLD");

        Result<VehicleDocumentResponse> result = await UpdateHandler().Handle(
            new UpdateVehicleDocumentCommand(_vehicle.Id, document.Id, "old", Remark: "noch gültig"),
            CancellationToken.None);

        result.Value.DocumentType.Should().Be("OLD");
    }

    [Fact]
    public async Task Update_DocumentOfAnotherVehicle_ReturnsNotFound()
    {
        VehicleDocument foreign = SeedDocument("ADR", vehicleId: Guid.CreateVersion7());

        Result<VehicleDocumentResponse> result = await UpdateHandler().Handle(
            new UpdateVehicleDocumentCommand(_vehicle.Id, foreign.Id, "ADR"), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Update_UnknownType_ReturnsFailureKeyedByDocumentType()
    {
        VehicleDocument document = SeedDocument("ADR");

        Result<VehicleDocumentResponse> result = await UpdateHandler().Handle(
            new UpdateVehicleDocumentCommand(_vehicle.Id, document.Id, "NOPE"), CancellationToken.None);

        result.Error!.Failures.Should().ContainKey("DocumentType");
    }

    [Fact]
    public async Task Update_InvalidDates_ReturnsDomainErrorAndDoesNotSave()
    {
        VehicleDocument document = SeedDocument("ADR");

        Result<VehicleDocumentResponse> result = await UpdateHandler().Handle(
            new UpdateVehicleDocumentCommand(
                _vehicle.Id, document.Id, "ADR", new DateOnly(2025, 2, 1), new DateOnly(2025, 1, 1)),
            CancellationToken.None);

        result.Error!.Code.Should().Be("VehicleDocument.ValidUntilBeforeLastChecked");
        _repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Delete_ExistingDocument_RemovesIt()
    {
        VehicleDocument document = SeedDocument("ADR");

        Result<Unit> result = await new DeleteVehicleDocumentCommandHandler(_repository).Handle(
            new DeleteVehicleDocumentCommand(_vehicle.Id, document.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repository.Documents.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_UnknownDocument_ReturnsNotFound()
    {
        Result<Unit> result = await new DeleteVehicleDocumentCommandHandler(_repository).Handle(
            new DeleteVehicleDocumentCommand(_vehicle.Id, Guid.CreateVersion7()), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task List_Documents_ReturnsSoonestExpiryFirstAndUndatedLast()
    {
        SeedDocument("UVV");
        SeedDocument("Tachograph", new DateOnly(2027, 1, 1));
        SeedDocument("ADR", new DateOnly(2025, 1, 1));
        SeedDocument("ATP", vehicleId: Guid.CreateVersion7());

        Result<VehicleDocumentResponse[]> result = await new ListVehicleDocumentsQueryHandler(_repository)
            .Handle(new ListVehicleDocumentsQuery(_vehicle.Id), CancellationToken.None);

        result.Value.Select(d => d.DocumentType).Should().Equal("ADR", "Tachograph", "UVV");
    }

    [Fact]
    public async Task List_UnknownVehicle_ReturnsNotFound()
    {
        Result<VehicleDocumentResponse[]> result = await new ListVehicleDocumentsQueryHandler(_repository)
            .Handle(new ListVehicleDocumentsQuery(Guid.CreateVersion7()), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Validator_InvalidFields_ReportsEachField()
    {
        ValidationResult result = new AddVehicleDocumentCommandValidator().Validate(new AddVehicleDocumentCommand(
            _vehicle.Id, "", new DateOnly(1899, 1, 1), new DateOnly(1899, 1, 1), new string('r', 501)));

        result.Errors.Select(e => e.PropertyName).Distinct().Should().BeEquivalentTo(
            ["DocumentType", "LastCheckedOn", "ValidUntil", "Remark"]);
    }

    [Fact]
    public void Validator_ExpiryBeforeLastCheck_ReportsValidUntil()
    {
        ValidationResult result = new UpdateVehicleDocumentCommandValidator().Validate(new UpdateVehicleDocumentCommand(
            _vehicle.Id, Guid.CreateVersion7(), "ADR", new DateOnly(2025, 2, 1), new DateOnly(2025, 1, 31)));

        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("ValidUntil");
    }

    [Fact]
    public void Validator_TypeOnly_IsValid()
    {
        new AddVehicleDocumentCommandValidator().Validate(new AddVehicleDocumentCommand(_vehicle.Id, "ADR"))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteVehicle_RemovesItsDocuments()
    {
        SeedDocument("ADR");

        await _repository.RemoveAsync(_vehicle, CancellationToken.None);

        _repository.Documents.Should().BeEmpty();
    }
}
