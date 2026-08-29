using AwesomeAssertions;
using TransBrain.Application.Features.Orders;
using TransBrain.Application.Features.Orders.CreateOrder;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Orders;

public class CreateOrderCommandHandlerTests
{
    private static AddressPayload AnAddress(string name) => new(name, "Hauptstr. 1", "80331", "München", "DE");

    private static CreateOrderCommand ValidCommand => new(
        AnAddress("Absender GmbH"),
        AnAddress("Empfänger AG"),
        "Palettenware",
        12_000,
        8.4m,
        new DateTimeOffset(2027, 3, 1, 8, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2027, 3, 1, 10, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2027, 3, 1, 12, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2027, 3, 1, 16, 0, 0, TimeSpan.Zero));

    private static CreateOrderCommandHandler Handler(
        InMemoryTransportOrderRepository repository,
        int firstSequence = 1)
        => new(repository, new StubOrderNumberGenerator(firstSequence), TimeProvider.System);

    [Fact]
    public async Task Handle_ValidCommand_PersistsOrderAndReturnsResponse()
    {
        InMemoryTransportOrderRepository repository = new();

        Result<OrderResponse> result = await Handler(repository).Handle(ValidCommand, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Draft");
        result.Value.Consignor.Name.Should().Be("Absender GmbH");
        result.Value.CargoWeightKg.Should().Be(12_000);
        repository.Orders.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_TwoOrders_TakesTheNumberFromTheGeneratorInSequence()
    {
        InMemoryTransportOrderRepository repository = new();
        CreateOrderCommandHandler handler = Handler(repository);

        Result<OrderResponse> first = await handler.Handle(ValidCommand, CancellationToken.None);
        Result<OrderResponse> second = await handler.Handle(ValidCommand, CancellationToken.None);

        first.Value.OrderNumber.Should().EndWith("-00001");
        second.Value.OrderNumber.Should().EndWith("-00002");
    }

    [Fact]
    public async Task Handle_BlankConsignorName_ReturnsDomainValidationErrorAndPersistsNothing()
    {
        InMemoryTransportOrderRepository repository = new();

        Result<OrderResponse> result = await Handler(repository).Handle(
            ValidCommand with { Consignor = AnAddress("   ") }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Address.NameRequired");
        repository.Orders.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_InvalidCountryCode_ReturnsDomainValidationError()
    {
        InMemoryTransportOrderRepository repository = new();

        Result<OrderResponse> result = await Handler(repository).Handle(
            ValidCommand with { Consignee = AnAddress("Empfänger AG") with { Country = "DEU" } },
            CancellationToken.None);

        result.Error!.Code.Should().Be("Address.CountryInvalid");
    }

    [Fact]
    public async Task Handle_NonPositiveCargoWeight_ReturnsDomainValidationError()
    {
        InMemoryTransportOrderRepository repository = new();

        Result<OrderResponse> result = await Handler(repository).Handle(
            ValidCommand with { CargoWeightKg = 0 }, CancellationToken.None);

        result.Error!.Code.Should().Be("Cargo.WeightKgNotPositive");
    }

    [Fact]
    public async Task Handle_PickupWindowEndsAfterItStarts_ReturnsDomainValidationError()
    {
        InMemoryTransportOrderRepository repository = new();

        Result<OrderResponse> result = await Handler(repository).Handle(
            ValidCommand with { PickupTo = ValidCommand.PickupFrom }, CancellationToken.None);

        result.Error!.Code.Should().Be("TimeWindow.FromNotBeforeTo");
    }

    [Fact]
    public async Task Handle_DeliveryStartsBeforePickupEnds_ReturnsDomainValidationError()
    {
        InMemoryTransportOrderRepository repository = new();

        Result<OrderResponse> result = await Handler(repository).Handle(
            ValidCommand with { DeliveryFrom = new DateTimeOffset(2027, 3, 1, 9, 0, 0, TimeSpan.Zero) },
            CancellationToken.None);

        result.Error!.Code.Should().Be("TransportOrder.DeliveryBeforePickupEnds");
    }
}
