using AwesomeAssertions;
using TransBrain.Application.Features.Geodata.DeleteGeodataOverride;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Application.Tests.Features.Geodata;

public class DeleteGeodataOverrideCommandHandlerTests
{
    [Fact]
    public async Task Handle_ExistingOverride_RemovesIt()
    {
        GeoOverride geoOverride = GeoOverride.Create(
            "Hauptstraße 1", "86150", "Augsburg", "DE", 48.37, 10.90, DateTimeOffset.UtcNow).Value;
        InMemoryGeoOverrideRepository repository = new InMemoryGeoOverrideRepository().Seed(geoOverride);
        DeleteGeodataOverrideCommandHandler handler = new(repository);

        Result<Unit> result = await handler.Handle(
            new DeleteGeodataOverrideCommand(geoOverride.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Items.Should().BeEmpty();
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnknownId_ReturnsNotFound()
    {
        DeleteGeodataOverrideCommandHandler handler = new(new InMemoryGeoOverrideRepository());

        Result<Unit> result = await handler.Handle(
            new DeleteGeodataOverrideCommand(Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("GeoOverride.NotFound");
    }
}
