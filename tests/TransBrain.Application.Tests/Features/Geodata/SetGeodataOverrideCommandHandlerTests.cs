using AwesomeAssertions;
using TransBrain.Application.Features.Geodata.SetGeodataOverride;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Application.Tests.Features.Geodata;

public class SetGeodataOverrideCommandHandlerTests
{
    private static SetGeodataOverrideCommand Command(double latitude = 48.37, double longitude = 10.90)
        => new("Hauptstraße 1", "86150", "Augsburg", "DE", latitude, longitude);

    [Fact]
    public async Task Handle_NewAddress_CreatesOverride()
    {
        InMemoryGeoOverrideRepository repository = new();
        SetGeodataOverrideCommandHandler handler = new(repository, new FixedTimeProvider());

        Result<GeodataOverrideResponse> result = await handler.Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Latitude.Should().Be(48.37);
        result.Value.Country.Should().Be("DE");
        result.Value.UpdatedAt.Should().Be(FixedTimeProvider.Default);
        repository.Items.Should().ContainSingle();
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ExistingAddress_RepositionsInsteadOfDuplicating()
    {
        InMemoryGeoOverrideRepository repository = new();
        SetGeodataOverrideCommandHandler handler = new(repository, new FixedTimeProvider());
        Guid firstId = (await handler.Handle(Command(), CancellationToken.None)).Value.Id;

        // Same address written differently: the key is case-insensitive and trimmed.
        Result<GeodataOverrideResponse> result = await handler.Handle(
            new SetGeodataOverrideCommand(" hauptstraße 1 ", "86150", "AUGSBURG", "de", 48.40, 10.95),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Items.Should().ContainSingle();
        result.Value.Id.Should().Be(firstId);
        result.Value.Latitude.Should().Be(48.40);
    }

    [Fact]
    public async Task Handle_InvalidCoordinates_ReturnsValidationAndStoresNothing()
    {
        InMemoryGeoOverrideRepository repository = new();
        SetGeodataOverrideCommandHandler handler = new(repository, new FixedTimeProvider());

        Result<GeodataOverrideResponse> result = await handler.Handle(Command(latitude: 123), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        repository.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_InvalidAddress_ReturnsValidation()
    {
        SetGeodataOverrideCommandHandler handler = new(new InMemoryGeoOverrideRepository(), new FixedTimeProvider());

        Result<GeodataOverrideResponse> result = await handler.Handle(
            new SetGeodataOverrideCommand("Hauptstraße 1", "86150", "Augsburg", "Deutschland", 48.37, 10.90),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Address.CountryInvalid");
    }
}
