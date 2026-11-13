using AwesomeAssertions;
using FluentValidation.Results;
using TransBrain.Application.Features.Orders.OrderCharges;
using TransBrain.Application.Features.Orders.OrderCharges.AddOrderCharge;
using TransBrain.Application.Features.Orders.OrderCharges.DeleteOrderCharge;
using TransBrain.Application.Features.Orders.OrderCharges.ListOrderCharges;
using TransBrain.Application.Features.Orders.OrderCharges.UpdateOrderCharge;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Orders;

public class OrderChargeHandlerTests
{
    private static readonly DateTimeOffset March1 = new(2027, 3, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Earlier = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly InMemoryTransportOrderRepository _repository = new();
    private readonly InMemoryVehicleCatalogRepository _catalog;
    private readonly TransportOrder _order;

    public OrderChargeHandlerTests()
    {
        _catalog = new InMemoryVehicleCatalogRepository(orders: _repository);
        _catalog.Seed(VehicleCatalogKind.Unit, "Std", "Stunde");
        _catalog.Seed(VehicleCatalogKind.Unit, "Stk", "Stück");
        _order = ADraftOrder();
        _repository.Seed(_order);
    }

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

    private AddOrderChargeCommandHandler AddHandler() =>
        new(_repository, _catalog, new StubCurrentUser("dispatch"), new FixedTimeProvider());

    private UpdateOrderChargeCommandHandler UpdateHandler() =>
        new(_repository, _catalog, new StubCurrentUser("dispatch"), new FixedTimeProvider());

    private OrderAdditionalCharge SeedCharge(
        string description = "Standzeit",
        decimal quantity = 1m,
        string? unit = "Std",
        decimal unitPrice = 0m,
        Guid? orderId = null)
    {
        OrderAdditionalCharge charge = OrderAdditionalCharge.Create(
            orderId ?? _order.Id, description, quantity, unit, unitPrice, "someone", Earlier).Value;
        _repository.SeedCharges(charge);
        return charge;
    }

    [Fact]
    public async Task Add_ValidCharge_PersistsItWithCanonicalUnitAndAuditStamp()
    {
        Result<OrderChargeResponse> result = await AddHandler().Handle(
            new AddOrderChargeCommand(_order.Id, "Wartezeit Rampe", 2.5m, "std", 45m), CancellationToken.None);

        result.Value.Should().BeEquivalentTo(new
        {
            OrderId = _order.Id,
            Description = "Wartezeit Rampe",
            Quantity = 2.5m,
            Unit = "Std",
            UnitPrice = 45m,
            Total = 112.5m,
            ModifiedAt = FixedTimeProvider.Default,
            ModifiedBy = "dispatch",
        });
        _repository.Charges.Should().ContainSingle().Which.Id.Should().Be(result.Value.Id);
    }

    [Fact]
    public async Task Add_WithoutUnit_Succeeds()
    {
        Result<OrderChargeResponse> result = await AddHandler().Handle(
            new AddOrderChargeCommand(_order.Id, "Maut pauschal", 1m, null, 25m), CancellationToken.None);

        result.Value.Unit.Should().BeNull();
    }

    [Fact]
    public async Task Add_UnknownOrder_ReturnsNotFound()
    {
        Result<OrderChargeResponse> result = await AddHandler().Handle(
            new AddOrderChargeCommand(Guid.CreateVersion7(), "Standzeit", 1m, null, 0m), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("TransportOrder.NotFound");
    }

    [Fact]
    public async Task Add_UnknownOrInactiveUnit_ReturnsFailureKeyedByUnit()
    {
        _catalog.Seed(VehicleCatalogKind.Unit, "OLD", "Veraltet", isActive: false);

        Result<OrderChargeResponse> unknown = await AddHandler().Handle(
            new AddOrderChargeCommand(_order.Id, "Standzeit", 1m, "NOPE", 0m), CancellationToken.None);
        Result<OrderChargeResponse> inactive = await AddHandler().Handle(
            new AddOrderChargeCommand(_order.Id, "Standzeit", 1m, "OLD", 0m), CancellationToken.None);

        unknown.Error!.Failures!["Unit"].Should().ContainSingle().Which.Should().Contain("unit");
        inactive.Error!.Failures.Should().ContainKey("Unit");
        _repository.Charges.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_ExistingCharge_ReplacesValuesAndRestamps()
    {
        OrderAdditionalCharge charge = SeedCharge();

        Result<OrderChargeResponse> result = await UpdateHandler().Handle(
            new UpdateOrderChargeCommand(_order.Id, charge.Id, "Palettentausch", 33m, "Stk", 8.5m),
            CancellationToken.None);

        result.Value.Should().BeEquivalentTo(new
        {
            Description = "Palettentausch",
            Quantity = 33m,
            Unit = "Stk",
            UnitPrice = 8.5m,
            Total = 280.5m,
            ModifiedBy = "dispatch",
            ModifiedAt = FixedTimeProvider.Default,
        });
    }

    [Fact]
    public async Task Update_ClearingTheUnit_IsAllowed()
    {
        OrderAdditionalCharge charge = SeedCharge();

        Result<OrderChargeResponse> result = await UpdateHandler().Handle(
            new UpdateOrderChargeCommand(_order.Id, charge.Id, "Standzeit", 1m, " ", 0m), CancellationToken.None);

        result.Value.Unit.Should().BeNull();
    }

    [Fact]
    public async Task Update_ChargeOfAnotherOrder_ReturnsNotFound()
    {
        OrderAdditionalCharge foreign = SeedCharge(orderId: Guid.CreateVersion7());

        Result<OrderChargeResponse> result = await UpdateHandler().Handle(
            new UpdateOrderChargeCommand(_order.Id, foreign.Id, "Standzeit", 1m, null, 0m), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("OrderAdditionalCharge.NotFound");
    }

    [Fact]
    public async Task Update_InvalidQuantity_ReturnsDomainErrorAndDoesNotSave()
    {
        OrderAdditionalCharge charge = SeedCharge();

        Result<OrderChargeResponse> result = await UpdateHandler().Handle(
            new UpdateOrderChargeCommand(_order.Id, charge.Id, "Standzeit", 0m, null, 0m), CancellationToken.None);

        result.Error!.Code.Should().Be("OrderAdditionalCharge.QuantityOutOfRange");
        _repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Delete_ExistingCharge_RemovesIt()
    {
        OrderAdditionalCharge charge = SeedCharge();

        Result<Unit> result = await new DeleteOrderChargeCommandHandler(_repository).Handle(
            new DeleteOrderChargeCommand(_order.Id, charge.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repository.Charges.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_UnknownCharge_ReturnsNotFound()
    {
        Result<Unit> result = await new DeleteOrderChargeCommandHandler(_repository).Handle(
            new DeleteOrderChargeCommand(_order.Id, Guid.CreateVersion7()), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task List_Charges_ReturnsOnlyTheOrdersCharges()
    {
        SeedCharge("Standzeit", 1m, "Std", 40m);
        SeedCharge("Palettentausch", 10m, "Stk", 8m);
        SeedCharge(orderId: Guid.CreateVersion7());

        Result<OrderChargeResponse[]> result = await new ListOrderChargesQueryHandler(_repository)
            .Handle(new ListOrderChargesQuery(_order.Id), CancellationToken.None);

        result.Value.Select(c => c.Description).Should().Equal("Standzeit", "Palettentausch");
    }

    [Fact]
    public async Task List_UnknownOrder_ReturnsNotFound()
    {
        Result<OrderChargeResponse[]> result = await new ListOrderChargesQueryHandler(_repository)
            .Handle(new ListOrderChargesQuery(Guid.CreateVersion7()), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Validator_InvalidFields_ReportsEachField()
    {
        ValidationResult result = new AddOrderChargeCommandValidator().Validate(new AddOrderChargeCommand(
            _order.Id, "", 0m, new string('u', 21), -1m));

        result.Errors.Select(e => e.PropertyName).Distinct().Should().BeEquivalentTo(
            ["Description", "Quantity", "Unit", "UnitPrice"]);
    }

    [Fact]
    public void Validator_DescriptionAndQuantityOnly_IsValid()
    {
        new AddOrderChargeCommandValidator().Validate(new AddOrderChargeCommand(_order.Id, "Standzeit", 1m, null, 0m))
            .IsValid.Should().BeTrue();
    }
}
