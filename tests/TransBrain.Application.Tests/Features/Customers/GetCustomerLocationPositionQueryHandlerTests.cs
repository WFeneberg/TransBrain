using AwesomeAssertions;
using TransBrain.Application.Features.Customers.CustomerLocations.GetCustomerLocationPosition;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;

namespace TransBrain.Application.Tests.Features.Customers;

public class GetCustomerLocationPositionQueryHandlerTests
{
    private static CustomerLocation LocationIn(string city, Guid customerId) => CustomerLocation.Create(
        customerId,
        "Lager Süd",
        Address.Create("Lager Süd", "Südbahnstraße 5", "1100", city, "AT").Value,
        null, null, null).Value;

    [Fact]
    public async Task Handle_ResolvableAddress_ReturnsPositionAndFormattedAddress()
    {
        Guid customerId = Guid.CreateVersion7();
        CustomerLocation location = LocationIn("Wien", customerId);
        InMemoryCustomerRepository repository = new();
        repository.SeedLocations(location);
        FakeGeocoder geocoder = new FakeGeocoder().Knows("Wien", 48.19, 16.37);
        GetCustomerLocationPositionQueryHandler handler = new(repository, geocoder, new InMemoryCacheService());

        Result<CustomerLocationPositionResponse> result = await handler.Handle(
            new GetCustomerLocationPositionQuery(customerId, location.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Latitude.Should().Be(48.19);
        result.Value.Longitude.Should().Be(16.37);
        result.Value.Address.Should().Be("Südbahnstraße 5, 1100 Wien, AT");
    }

    [Fact]
    public async Task Handle_UnknownAddress_ReturnsNotFound()
    {
        Guid customerId = Guid.CreateVersion7();
        CustomerLocation location = LocationIn("Atlantis", customerId);
        InMemoryCustomerRepository repository = new();
        repository.SeedLocations(location);
        GetCustomerLocationPositionQueryHandler handler = new(repository, new FakeGeocoder(), new InMemoryCacheService());

        Result<CustomerLocationPositionResponse> result = await handler.Handle(
            new GetCustomerLocationPositionQuery(customerId, location.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_GeocoderNotConfigured_PassesUnavailableThrough()
    {
        Guid customerId = Guid.CreateVersion7();
        CustomerLocation location = LocationIn("Wien", customerId);
        InMemoryCustomerRepository repository = new();
        repository.SeedLocations(location);
        FakeGeocoder geocoder = new() { Failure = Error.Unavailable("Routing.NotConfigured", "not configured") };
        GetCustomerLocationPositionQueryHandler handler = new(repository, geocoder, new InMemoryCacheService());

        Result<CustomerLocationPositionResponse> result = await handler.Handle(
            new GetCustomerLocationPositionQuery(customerId, location.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Unavailable);
    }

    [Fact]
    public async Task Handle_UnknownLocation_ReturnsNotFound()
    {
        GetCustomerLocationPositionQueryHandler handler = new(
            new InMemoryCustomerRepository(), new FakeGeocoder(), new InMemoryCacheService());

        Result<CustomerLocationPositionResponse> result = await handler.Handle(
            new GetCustomerLocationPositionQuery(Guid.CreateVersion7(), Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("CustomerLocation.NotFound");
    }
}
