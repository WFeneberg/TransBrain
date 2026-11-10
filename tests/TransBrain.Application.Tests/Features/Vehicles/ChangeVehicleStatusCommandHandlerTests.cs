using AwesomeAssertions;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Application.Features.Vehicles.ChangeVehicleStatus;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Vehicles;

public class ChangeVehicleStatusCommandHandlerTests
{
    private static Vehicle ExistingVehicle() =>
        Vehicle.Create(LicensePlate.Create("M-AB 1234").Value, VehicleType.SemiTrailer, 3_000, 4.0m, new DateOnly(2027, 3, 31)).Value;

    private static ChangeVehicleStatusCommandHandler CreateHandler(
        InMemoryVehicleRepository repository, InMemoryCacheService cache) =>
        new(repository, cache, new StubCurrentUser("werkstatt"), new FixedTimeProvider());

    [Fact]
    public async Task Handle_AvailableToWorkshop_ChangesStatusStampsAndInvalidatesCache()
    {
        InMemoryVehicleRepository repository = new();
        Vehicle vehicle = ExistingVehicle();
        repository.Seed(vehicle);
        InMemoryCacheService cache = new();
        ChangeVehicleStatusCommandHandler handler = CreateHandler(repository, cache);

        Result<VehicleResponse> result = await handler.Handle(
            new ChangeVehicleStatusCommand(vehicle.Id, "inworkshop"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("InWorkshop");
        result.Value.ModifiedBy.Should().Be("werkstatt");
        result.Value.ModifiedAt.Should().Be(FixedTimeProvider.Default);
        repository.SaveChangesCallCount.Should().Be(1);
        cache.RemoveByPrefixCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SameStatus_ReturnsVehicleWithoutSaving()
    {
        InMemoryVehicleRepository repository = new();
        Vehicle vehicle = ExistingVehicle();
        repository.Seed(vehicle);
        InMemoryCacheService cache = new();
        ChangeVehicleStatusCommandHandler handler = CreateHandler(repository, cache);

        Result<VehicleResponse> result = await handler.Handle(
            new ChangeVehicleStatusCommand(vehicle.Id, "Available"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ModifiedAt.Should().BeNull();
        repository.SaveChangesCallCount.Should().Be(0);
        cache.RemoveByPrefixCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_DecommissionedVehicleBackToService_ReturnsConflictAndDoesNotSave()
    {
        InMemoryVehicleRepository repository = new();
        Vehicle vehicle = ExistingVehicle();
        vehicle.Decommission();
        repository.Seed(vehicle);
        ChangeVehicleStatusCommandHandler handler = CreateHandler(repository, new InMemoryCacheService());

        Result<VehicleResponse> result = await handler.Handle(
            new ChangeVehicleStatusCommand(vehicle.Id, "Available"), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Vehicle.Decommissioned");
        vehicle.Status.Should().Be(VehicleStatus.Decommissioned);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_UnknownVehicle_ReturnsNotFound()
    {
        ChangeVehicleStatusCommandHandler handler = CreateHandler(new InMemoryVehicleRepository(), new InMemoryCacheService());

        Result<VehicleResponse> result = await handler.Handle(
            new ChangeVehicleStatusCommand(Guid.CreateVersion7(), "InWorkshop"), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Theory]
    [InlineData("Scrapped")]
    [InlineData("7")]
    [InlineData("")]
    public async Task Handle_UnknownStatus_ReturnsValidationError(string status)
    {
        InMemoryVehicleRepository repository = new();
        Vehicle vehicle = ExistingVehicle();
        repository.Seed(vehicle);
        ChangeVehicleStatusCommandHandler handler = CreateHandler(repository, new InMemoryCacheService());

        Result<VehicleResponse> result = await handler.Handle(
            new ChangeVehicleStatusCommand(vehicle.Id, status), CancellationToken.None);

        result.Error!.Code.Should().Be("Vehicle.UnknownStatus");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Theory]
    [InlineData("Decommissioned", true)]
    [InlineData("available", true)]
    [InlineData("Scrapped", false)]
    [InlineData("", false)]
    public void Validator_Status_AcceptsOnlyKnownStatuses(string status, bool valid)
    {
        ChangeVehicleStatusCommandValidator validator = new();

        validator.Validate(new ChangeVehicleStatusCommand(Guid.CreateVersion7(), status)).IsValid.Should().Be(valid);
    }
}
