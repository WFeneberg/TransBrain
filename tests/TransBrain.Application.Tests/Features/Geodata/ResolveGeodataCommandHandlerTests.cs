using AwesomeAssertions;
using TransBrain.Application.Features.Geodata;
using TransBrain.Application.Features.Geodata.ResolveGeodata;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Application.Tests.Features.Geodata;

public class ResolveGeodataCommandHandlerTests
{
    private static ResolveGeodataCommand Augsburg()
        => new("Hauptstraße 1", "86150", "Augsburg", "DE");

    [Fact]
    public async Task Handle_OverrideExists_AnswersManualWithoutGeocoding()
    {
        GeoOverride manual = GeoOverride.Create(
            "Hauptstraße 1", "86150", "Augsburg", "DE", 48.37, 10.90, DateTimeOffset.UtcNow).Value;
        InMemoryGeoOverrideRepository overrides = new InMemoryGeoOverrideRepository().Seed(manual);
        // A geocoder that would fail loudly: it must not be consulted.
        FakeGeocoder geocoder = new() { Failure = Error.Unavailable("Geocoding.Unreachable", "down") };
        ResolveGeodataCommandHandler handler = new(overrides, geocoder, new InMemoryCacheService());

        Result<GeodataPositionResponse> result = await handler.Handle(Augsburg(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Source.Should().Be(GeodataSources.Manual);
        result.Value.Latitude.Should().Be(48.37);
    }

    [Fact]
    public async Task Handle_GeocoderKnowsAddress_AnswersGeocodedAndFillsCache()
    {
        InMemoryCacheService cache = new();
        FakeGeocoder geocoder = new FakeGeocoder().Knows("Augsburg", 48.37, 10.90);
        ResolveGeodataCommandHandler handler = new(new InMemoryGeoOverrideRepository(), geocoder, cache);

        Result<GeodataPositionResponse> result = await handler.Handle(Augsburg(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Source.Should().Be(GeodataSources.Geocoded);
        (await cache.GetAsync<TransBrain.Application.Features.Routing.GeocodeCache.CachedPoint>(
            "geocode:de|86150|augsburg|hauptstraße 1", CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_UnknownAddress_ReturnsNotFound()
    {
        ResolveGeodataCommandHandler handler = new(
            new InMemoryGeoOverrideRepository(), new FakeGeocoder(), new InMemoryCacheService());

        Result<GeodataPositionResponse> result = await handler.Handle(Augsburg(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_InvalidAddress_ReturnsValidation()
    {
        ResolveGeodataCommandHandler handler = new(
            new InMemoryGeoOverrideRepository(), new FakeGeocoder(), new InMemoryCacheService());

        Result<GeodataPositionResponse> result = await handler.Handle(
            new ResolveGeodataCommand("Hauptstraße 1", "86150", "Augsburg", "XYZ"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Validation);
    }
}
