using AwesomeAssertions;
using TransBrain.Application.Features.Orders;
using TransBrain.Application.Features.Orders.UpdateOrder;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Tests.Features.Orders;

public class UpdateOrderCommandHandlerTests
{
    private static readonly DateTimeOffset March1 = new(2027, 3, 1, 8, 0, 0, TimeSpan.Zero);

    private static AddressPayload AnAddress(string name) => new(name, "Hauptstr. 1", "80331", "München", "DE");

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

    private static UpdateOrderCommand CommandFor(Guid id) => new(
        id,
        AnAddress("Neuer Absender GmbH"),
        AnAddress("Neuer Empfänger AG"),
        "Kühlware",
        9_000,
        6.2m,
        March1.AddDays(1),
        March1.AddDays(1).AddHours(2),
        March1.AddDays(1).AddHours(4),
        March1.AddDays(1).AddHours(8));

    [Fact]
    public async Task Handle_DraftOrder_UpdatesFieldsAndSavesOnce()
    {
        InMemoryTransportOrderRepository repository = new();
        TransportOrder order = ADraftOrder();
        repository.Seed(order);
        UpdateOrderCommandHandler handler = new(repository);

        Result<OrderResponse> result = await handler.Handle(CommandFor(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Consignor.Name.Should().Be("Neuer Absender GmbH");
        result.Value.CargoDescription.Should().Be("Kühlware");
        result.Value.CargoWeightKg.Should().Be(9_000);
        result.Value.PickupFrom.Should().Be(March1.AddDays(1));
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnknownOrder_ReturnsNotFoundAndDoesNotSave()
    {
        InMemoryTransportOrderRepository repository = new();
        UpdateOrderCommandHandler handler = new(repository);

        Result<OrderResponse> result = await handler.Handle(
            CommandFor(Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("TransportOrder.NotFound");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PlannedOrder_ReturnsConflictAndDoesNotSave()
    {
        InMemoryTransportOrderRepository repository = new();
        TransportOrder order = ADraftOrder();
        order.MarkPlanned();
        repository.Seed(order);
        UpdateOrderCommandHandler handler = new(repository);

        Result<OrderResponse> result = await handler.Handle(CommandFor(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("TransportOrder.NotEditable");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_InvalidAddress_ReturnsDomainValidationErrorAndDoesNotSave()
    {
        InMemoryTransportOrderRepository repository = new();
        TransportOrder order = ADraftOrder();
        repository.Seed(order);
        UpdateOrderCommandHandler handler = new(repository);

        UpdateOrderCommand command = CommandFor(order.Id) with { Consignor = AnAddress("   ") };

        Result<OrderResponse> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Address.NameRequired");
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_OverlappingWindows_ReturnsDomainValidationErrorAndLeavesOrderUnchanged()
    {
        InMemoryTransportOrderRepository repository = new();
        TransportOrder order = ADraftOrder();
        repository.Seed(order);
        UpdateOrderCommandHandler handler = new(repository);

        // Delivery starts an hour before the pickup window ends.
        UpdateOrderCommand command = CommandFor(order.Id) with
        {
            PickupFrom = March1,
            PickupTo = March1.AddHours(4),
            DeliveryFrom = March1.AddHours(3),
            DeliveryTo = March1.AddHours(8)
        };

        Result<OrderResponse> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("TransportOrder.DeliveryBeforePickupEnds");
        order.Cargo.Description.Should().Be("Palettenware");
        repository.SaveChangesCallCount.Should().Be(0);
    }
}
