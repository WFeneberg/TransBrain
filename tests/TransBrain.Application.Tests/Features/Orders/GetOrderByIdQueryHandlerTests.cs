using AwesomeAssertions;
using TransBrain.Application.Features.Orders;
using TransBrain.Application.Features.Orders.GetOrderById;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Tests.Features.Orders;

public class GetOrderByIdQueryHandlerTests
{
    private static TransportOrder AnOrder()
    {
        DateTimeOffset pickupFrom = new(2027, 3, 1, 8, 0, 0, TimeSpan.Zero);
        Address address = Address.Create("Absender GmbH", "Hauptstr. 1", "80331", "München", "DE").Value;

        return TransportOrder.Create(
            OrderNumber.From(2027, 1),
            address,
            address,
            Cargo.Create("Palettenware", 12_000, 8.4m).Value,
            TimeWindow.Create(pickupFrom, pickupFrom.AddHours(2)).Value,
            TimeWindow.Create(pickupFrom.AddHours(4), pickupFrom.AddHours(8)).Value,
            pickupFrom.AddDays(-30)).Value;
    }

    [Fact]
    public async Task Handle_KnownId_ReturnsOrder()
    {
        InMemoryTransportOrderRepository repository = new();
        TransportOrder order = AnOrder();
        repository.Seed(order);
        GetOrderByIdQueryHandler handler = new(repository);

        Result<OrderResponse> result = await handler.Handle(
            new GetOrderByIdQuery(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(order.Id);
        result.Value.OrderNumber.Should().Be("TB-2027-00001");
    }

    [Fact]
    public async Task Handle_UnknownId_ReturnsNotFound()
    {
        GetOrderByIdQueryHandler handler = new(new InMemoryTransportOrderRepository());

        Result<OrderResponse> result = await handler.Handle(
            new GetOrderByIdQuery(Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("TransportOrder.NotFound");
    }
}
