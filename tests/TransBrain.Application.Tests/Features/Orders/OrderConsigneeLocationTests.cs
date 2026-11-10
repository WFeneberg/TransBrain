using AwesomeAssertions;
using TransBrain.Application.Features.Orders;
using TransBrain.Application.Features.Orders.CreateOrder;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;

namespace TransBrain.Application.Tests.Features.Orders;

public class OrderConsigneeLocationTests
{
    private static CreateOrderCommand Command(Guid? consigneeLocationId) => new(
        new AddressPayload("Absender GmbH", "Weg 1", "4020", "Linz", "AT"),
        new AddressPayload("Empfänger AG", "Ringstraße 9", "1010", "Wien", "AT"),
        "Paletten",
        1200,
        4.5m,
        new DateTimeOffset(2027, 3, 1, 8, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2027, 3, 1, 10, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2027, 3, 1, 16, 0, 0, TimeSpan.Zero),
        consigneeLocationId);

    [Fact]
    public async Task Create_WithKnownLocation_StoresLocationReferenceOnOrder()
    {
        InMemoryCustomerRepository customers = new();
        CustomerLocation location = CustomerLocation.Create(
            Guid.CreateVersion7(),
            "Lager Wien",
            Address.Create("Lager Wien", "Südbahnstraße 5", "1100", "Wien", "AT").Value,
            []).Value;
        customers.SeedLocations(location);
        InMemoryTransportOrderRepository orders = new();
        CreateOrderCommandHandler handler = new(orders, new StubOrderNumberGenerator(1), customers, TimeProvider.System);

        Result<OrderResponse> result = await handler.Handle(Command(location.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ConsigneeLocationId.Should().Be(location.Id);
    }

    [Fact]
    public async Task Create_WithoutLocation_LeavesReferenceEmpty()
    {
        CreateOrderCommandHandler handler = new(
            new InMemoryTransportOrderRepository(),
            new StubOrderNumberGenerator(1),
            new InMemoryCustomerRepository(),
            TimeProvider.System);

        Result<OrderResponse> result = await handler.Handle(Command(null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ConsigneeLocationId.Should().BeNull();
    }

    [Fact]
    public async Task Create_WithUnknownLocation_ReturnsNotFoundAndStoresNothing()
    {
        InMemoryTransportOrderRepository orders = new();
        CreateOrderCommandHandler handler = new(
            orders,
            new StubOrderNumberGenerator(1),
            new InMemoryCustomerRepository(),
            TimeProvider.System);

        Result<OrderResponse> result = await handler.Handle(Command(Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("CustomerLocation.NotFound");
        orders.Orders.Should().BeEmpty();
    }
}
