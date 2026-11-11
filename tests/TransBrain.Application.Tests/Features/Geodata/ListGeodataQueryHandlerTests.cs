using AwesomeAssertions;
using TransBrain.Application.Features.Geodata;
using TransBrain.Application.Features.Geodata.ListGeodata;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Application.Tests.Features.Geodata;

public class ListGeodataQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);

    private static UsedAddress Augsburg(int orders = 2, int locations = 1)
        => new("Hauptstraße 1", "86150", "Augsburg", "DE", orders, locations);

    [Fact]
    public async Task Handle_AddressWithOverride_ReportsManualSourceAndOverrideId()
    {
        GeoOverride manual = GeoOverride.Create("Hauptstraße 1", "86150", "Augsburg", "DE", 48.37, 10.90, Now).Value;
        InMemoryGeoOverrideRepository repository = new InMemoryGeoOverrideRepository()
            .Seed(manual)
            .SeedUsedAddress(Augsburg());
        ListGeodataQueryHandler handler = new(repository, new InMemoryCacheService());

        Result<GeodataEntryResponse[]> result = await handler.Handle(new ListGeodataQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        GeodataEntryResponse entry = result.Value.Single();
        entry.Source.Should().Be(GeodataSources.Manual);
        entry.OverrideId.Should().Be(manual.Id);
        entry.Latitude.Should().Be(48.37);
        entry.OrderCount.Should().Be(2);
        entry.LocationCount.Should().Be(1);
        entry.InUse.Should().BeTrue();
        entry.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Handle_AddressOnlyInGeocodeCache_ReportsGeocodedSource()
    {
        InMemoryGeoOverrideRepository repository = new InMemoryGeoOverrideRepository()
            .SeedUsedAddress(Augsburg());
        InMemoryCacheService cache = new();
        // The same shape and key the LocationResolver writes.
        await cache.SetAsync(
            "geocode:de|86150|augsburg|hauptstraße 1",
            new TransBrain.Application.Features.Routing.GeocodeCache.CachedPoint(48.37, 10.90),
            CancellationToken.None);
        ListGeodataQueryHandler handler = new(repository, cache);

        Result<GeodataEntryResponse[]> result = await handler.Handle(new ListGeodataQuery(), CancellationToken.None);

        GeodataEntryResponse entry = result.Value.Single();
        entry.Source.Should().Be(GeodataSources.Geocoded);
        entry.OverrideId.Should().BeNull();
        entry.Latitude.Should().Be(48.37);
    }

    [Fact]
    public async Task Handle_UnknownAddress_ReportsUnresolvedWithoutCoordinates()
    {
        InMemoryGeoOverrideRepository repository = new InMemoryGeoOverrideRepository()
            .SeedUsedAddress(Augsburg());
        ListGeodataQueryHandler handler = new(repository, new InMemoryCacheService());

        Result<GeodataEntryResponse[]> result = await handler.Handle(new ListGeodataQuery(), CancellationToken.None);

        GeodataEntryResponse entry = result.Value.Single();
        entry.Source.Should().Be(GeodataSources.Unresolved);
        entry.Latitude.Should().BeNull();
        entry.Longitude.Should().BeNull();
    }

    [Fact]
    public async Task Handle_OverrideForUnusedAddress_StaysListedAsNotInUse()
    {
        GeoOverride orphan = GeoOverride.Create("Alte Gasse 9", "10115", "Berlin", "DE", 52.53, 13.38, Now).Value;
        InMemoryGeoOverrideRepository repository = new InMemoryGeoOverrideRepository().Seed(orphan);
        ListGeodataQueryHandler handler = new(repository, new InMemoryCacheService());

        Result<GeodataEntryResponse[]> result = await handler.Handle(new ListGeodataQuery(), CancellationToken.None);

        GeodataEntryResponse entry = result.Value.Single();
        entry.InUse.Should().BeFalse();
        entry.Source.Should().Be(GeodataSources.Manual);
        entry.OrderCount.Should().Be(0);
        entry.LocationCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_MixedEntries_SortsByCountryCityStreet()
    {
        InMemoryGeoOverrideRepository repository = new InMemoryGeoOverrideRepository()
            .SeedUsedAddress(new UsedAddress("Weg 2", "5020", "Salzburg", "AT", 1, 0))
            .SeedUsedAddress(new UsedAddress("Zeil 1", "60313", "Frankfurt", "DE", 1, 0))
            .SeedUsedAddress(new UsedAddress("Allee 3", "86150", "Augsburg", "DE", 1, 0));
        ListGeodataQueryHandler handler = new(repository, new InMemoryCacheService());

        Result<GeodataEntryResponse[]> result = await handler.Handle(new ListGeodataQuery(), CancellationToken.None);

        result.Value.Select(e => e.City).Should().Equal("Salzburg", "Augsburg", "Frankfurt");
    }
}
