using AwesomeAssertions;
using TransBrain.Application.Features.Orders;
using TransBrain.Application.Features.Orders.CancelOrder;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Tests.Features.Orders;

public class CancelOrderCommandHandlerTests
{
    private static readonly DateTimeOffset March1 = new(2027, 3, 1, 8, 0, 0, TimeSpan.Zero);

    private static TransportOrder ADraftOrder()
    {
        Address address = Address.Create("Absender GmbH", "Hauptstr. 1", "80331", "München", "DE").Value;

        return TransportOrder.Create(
            OrderNumber.From(2027, 1),
            address,
            address,
            Cargo.Create("Palettenware", 12_000, 8.4m).Value,
            TimeWindow.Create(March1, March1.AddHours(2)).Value,
            TimeWindow.Create(March1.AddHours(4), March1.AddHours(8)).Value,
            March1.AddDays(-30)).Value;
    }

    [Fact]
    public async Task Handle_DraftOrder_CancelsAndSavesOnce()
    {
        InMemoryTransportOrderRepository repository = new();
        TransportOrder order = ADraftOrder();
        repository.Seed(order);
        CancelOrderCommandHandler handler = new(repository);

        Result<OrderResponse> result = await handler.Handle(
            new CancelOrderCommand(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Cancelled");
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_PlannedOrder_CancelsAndSavesOnce()
    {
        InMemoryTransportOrderRepository repository = new();
        TransportOrder order = ADraftOrder();
        order.MarkPlanned();
        repository.Seed(order);
        CancelOrderCommandHandler handler = new(repository);

        Result<OrderResponse> result = await handler.Handle(
            new CancelOrderCommand(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Cancelled");
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_InTransitOrder_ReturnsConflictAndDoesNotSave()
    {
        InMemoryTransportOrderRepository repository = new();
        TransportOrder order = ADraftOrder();
        order.MarkPlanned();
        order.MarkInTransit();
        repository.Seed(order);
        CancelOrderCommandHandler handler = new(repository);

        Result<OrderResponse> result = await handler.Handle(
            new CancelOrderCommand(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("TransportOrder.InvalidTransition");
        order.Status.Should().Be(OrderStatus.InTransit);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_UnknownOrder_ReturnsNotFoundAndDoesNotSave()
    {
        InMemoryTransportOrderRepository repository = new();
        CancelOrderCommandHandler handler = new(repository);

        Result<OrderResponse> result = await handler.Handle(
            new CancelOrderCommand(Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("TransportOrder.NotFound");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_AlreadyCancelledOrder_ReturnsConflict()
    {
        InMemoryTransportOrderRepository repository = new();
        TransportOrder order = ADraftOrder();
        order.Cancel();
        repository.Seed(order);
        CancelOrderCommandHandler handler = new(repository);

        Result<OrderResponse> result = await handler.Handle(
            new CancelOrderCommand(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        repository.SaveChangesCallCount.Should().Be(0);
    }
}
