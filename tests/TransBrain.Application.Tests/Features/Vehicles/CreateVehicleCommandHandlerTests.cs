using AwesomeAssertions;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Application.Features.Vehicles.CreateVehicle;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Vehicles;

public class CreateVehicleCommandHandlerTests
{
    private static CreateVehicleCommand ValidCommand => new("M-AB 1234", "Tractor", 24_000, 13.6m, new DateOnly(2027, 3, 31));

    [Fact]
    public async Task Handle_ValidCommand_PersistsVehicleAndReturnsResponse()
    {
        InMemoryVehicleRepository repository = new();
        CreateVehicleCommandHandler handler = new(repository);

        Result<VehicleResponse> result = await handler.Handle(ValidCommand, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.LicensePlate.Should().Be("M-AB 1234");
        result.Value.Status.Should().Be("Available");
        repository.Vehicles.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_DuplicateLicensePlate_ReturnsConflictError()
    {
        InMemoryVehicleRepository repository = new();
        repository.Seed(Vehicle.Create(
            LicensePlate.Create("M-AB 1234").Value, VehicleType.Tractor, 24_000, 13.6m, new DateOnly(2027, 3, 31)).Value);
        CreateVehicleCommandHandler handler = new(repository);

        Result<VehicleResponse> result = await handler.Handle(ValidCommand, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Vehicle.DuplicateLicensePlate");
        repository.Vehicles.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_UnknownVehicleType_ReturnsValidationError()
    {
        InMemoryVehicleRepository repository = new();
        CreateVehicleCommandHandler handler = new(repository);

        Result<VehicleResponse> result = await handler.Handle(
            ValidCommand with { Type = "Spaceship" }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Vehicle.UnknownType");
    }

    [Fact]
    public async Task Handle_NumericUndefinedVehicleType_ReturnsValidationError()
    {
        // Enum.TryParse alone happily parses a numeric string into the underlying integer
        // value even when no VehicleType member defines it (e.g. (VehicleType)99), which would
        // then pass Vehicle.Create and be persisted as the literal string "99". This guards the
        // fix (Enum.TryParse combined with Enum.IsDefined) that rejects it before it gets there.
        InMemoryVehicleRepository repository = new();
        CreateVehicleCommandHandler handler = new(repository);

        Result<VehicleResponse> result = await handler.Handle(
            ValidCommand with { Type = "99" }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Vehicle.UnknownType");
        repository.Vehicles.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_InvalidLicensePlate_ReturnsDomainValidationError()
    {
        InMemoryVehicleRepository repository = new();
        CreateVehicleCommandHandler handler = new(repository);

        Result<VehicleResponse> result = await handler.Handle(
            ValidCommand with { LicensePlate = "   " }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("LicensePlate.Empty");
    }

    [Fact]
    public async Task Handle_NonPositivePayload_ReturnsDomainValidationError()
    {
        InMemoryVehicleRepository repository = new();
        CreateVehicleCommandHandler handler = new(repository);

        Result<VehicleResponse> result = await handler.Handle(
            ValidCommand with { PayloadKg = 0 }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Vehicle.PayloadKgNotPositive");
    }
}
