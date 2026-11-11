using AwesomeAssertions;
using TransBrain.Application.Features.Orders;
using TransBrain.Application.Features.Orders.GetOrderLocations;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Application.Tests.Features.Tours;

namespace TransBrain.Application.Tests.Features.Orders;

public class GetOrderLocationsQueryHandlerTests
{
    private static GetOrderLocationsQueryHandler Handler(
        InMemoryTransportOrderRepository orders,
        FakeGeocoder geocoder,
        InMemoryGeoOverrideRepository? overrides = null) =>
        new(orders, overrides ?? new InMemoryGeoOverrideRepository(), geocoder, new InMemoryCacheService());

    [Fact]
    public async Task Handle_BothAddressesLocatable_ReturnsBothPlaces()
    {
        InMemoryTransportOrderRepository orders = new();
        var order = TourFixture.AnOrder();
        orders.Seed(order);
        FakeGeocoder geocoder = new FakeGeocoder()
            .Knows("München", 48.137, 11.575)
            .Knows("Berlin", 52.520, 13.405);

        Result<OrderLocationsResponse> result = await Handler(orders, geocoder)
            .Handle(new GetOrderLocationsQuery(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Consignor!.Latitude.Should().Be(48.137);
        result.Value.Consignee!.Name.Should().Be("Empfaenger AG");
    }

    [Fact]
    public async Task Handle_ConsigneeMatchesNoPlace_ReturnsNullForConsigneeOnly()
    {
        InMemoryTransportOrderRepository orders = new();
        var order = TourFixture.AnOrder();
        orders.Seed(order);
        FakeGeocoder geocoder = new FakeGeocoder().Knows("München", 48.137, 11.575);

        Result<OrderLocationsResponse> result = await Handler(orders, geocoder)
            .Handle(new GetOrderLocationsQuery(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Consignor.Should().NotBeNull();
        result.Value.Consignee.Should().BeNull();
    }

    [Fact]
    public async Task Handle_GeocoderNotConfigured_ReturnsUnavailable()
    {
        InMemoryTransportOrderRepository orders = new();
        var order = TourFixture.AnOrder();
        orders.Seed(order);
        FakeGeocoder geocoder = new() { Failure = Error.Unavailable("Routing.NotConfigured", "Not set.") };

        Result<OrderLocationsResponse> result = await Handler(orders, geocoder)
            .Handle(new GetOrderLocationsQuery(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Unavailable);
    }

    [Fact]
    public async Task Handle_UnknownOrder_ReturnsNotFound()
    {
        Result<OrderLocationsResponse> result = await Handler(new InMemoryTransportOrderRepository(), new FakeGeocoder())
            .Handle(new GetOrderLocationsQuery(Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("TransportOrder.NotFound");
    }
}
