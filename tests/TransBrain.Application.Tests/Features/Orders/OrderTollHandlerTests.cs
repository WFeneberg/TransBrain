using AwesomeAssertions;
using FluentValidation.Results;
using TransBrain.Application.Features.Orders.OrderTolls;
using TransBrain.Application.Features.Orders.OrderTolls.AddOrderToll;
using TransBrain.Application.Features.Orders.OrderTolls.DeleteOrderToll;
using TransBrain.Application.Features.Orders.OrderTolls.ListOrderTolls;
using TransBrain.Application.Features.Orders.OrderTolls.UpdateOrderToll;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Orders;

public class OrderTollHandlerTests
{
    private static readonly DateTimeOffset March1 = new(2027, 3, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Earlier = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly InMemoryTransportOrderRepository _repository = new();
    private readonly InMemoryVehicleCatalogRepository _catalog;
    private readonly TransportOrder _order;

    public OrderTollHandlerTests()
    {
        _catalog = new InMemoryVehicleCatalogRepository(orders: _repository);
        _catalog.Seed(VehicleCatalogKind.TollCategory, "D-015", "D 0,15");
        _catalog.Seed(VehicleCatalogKind.TollCategory, "A-0300", "A 0,300");
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

    private AddOrderTollCommandHandler AddHandler() =>
        new(_repository, _catalog, new StubCurrentUser("dispatch"), new FixedTimeProvider());

    private UpdateOrderTollCommandHandler UpdateHandler() =>
        new(_repository, _catalog, new StubCurrentUser("dispatch"), new FixedTimeProvider());

    private OrderTollCharge SeedToll(
        string category = "D-015", int emptyKm = 0, int loadedKm = 0, decimal factor = 0m, Guid? orderId = null)
    {
        OrderTollCharge toll = OrderTollCharge.Create(
            orderId ?? _order.Id, category, emptyKm, loadedKm, factor, "someone", Earlier).Value;
        _repository.SeedTolls(toll);
        return toll;
    }

    [Fact]
    public async Task Add_ValidToll_PersistsItWithCanonicalCategoryAndAuditStamp()
    {
        Result<OrderTollResponse> result = await AddHandler().Handle(
            new AddOrderTollCommand(_order.Id, "d-015", 120, 480, 0.15m), CancellationToken.None);

        result.Value.Should().BeEquivalentTo(new
        {
            OrderId = _order.Id,
            TollCategory = "D-015",
            EmptyKm = 120,
            LoadedKm = 480,
            FactorPerKm = 0.15m,
            EmptyAmount = 18m,
            LoadedAmount = 72m,
            ModifiedAt = FixedTimeProvider.Default,
            ModifiedBy = "dispatch",
        });
        _repository.Tolls.Should().ContainSingle().Which.Id.Should().Be(result.Value.Id);
    }

    [Fact]
    public async Task Add_UnknownOrder_ReturnsNotFound()
    {
        Result<OrderTollResponse> result = await AddHandler().Handle(
            new AddOrderTollCommand(Guid.CreateVersion7(), "D-015", 0, 0, 0m), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("TransportOrder.NotFound");
    }

    [Fact]
    public async Task Add_UnknownOrInactiveCategory_ReturnsFailureKeyedByTollCategory()
    {
        _catalog.Seed(VehicleCatalogKind.TollCategory, "OLD", "Veraltet", isActive: false);

        Result<OrderTollResponse> unknown = await AddHandler().Handle(
            new AddOrderTollCommand(_order.Id, "NOPE", 0, 0, 0m), CancellationToken.None);
        Result<OrderTollResponse> inactive = await AddHandler().Handle(
            new AddOrderTollCommand(_order.Id, "OLD", 0, 0, 0m), CancellationToken.None);

        unknown.Error!.Failures!["TollCategory"].Should().ContainSingle().Which.Should().Contain("toll category");
        inactive.Error!.Failures.Should().ContainKey("TollCategory");
        _repository.Tolls.Should().BeEmpty();
    }

    [Fact]
    public async Task Add_NonDraftOrder_StillSucceeds()
    {
        _order.MarkPlanned();
        _order.MarkInTransit();
        _order.MarkDelivered();

        Result<OrderTollResponse> result = await AddHandler().Handle(
            new AddOrderTollCommand(_order.Id, "D-015", 10, 20, 0.15m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Update_ExistingToll_ReplacesValuesAndRestamps()
    {
        OrderTollCharge toll = SeedToll();

        Result<OrderTollResponse> result = await UpdateHandler().Handle(
            new UpdateOrderTollCommand(_order.Id, toll.Id, "A-0300", 33, 67, 0.3m), CancellationToken.None);

        result.Value.Should().BeEquivalentTo(new
        {
            TollCategory = "A-0300",
            EmptyKm = 33,
            LoadedKm = 67,
            FactorPerKm = 0.3m,
            EmptyAmount = 9.9m,
            LoadedAmount = 20.1m,
            ModifiedBy = "dispatch",
            ModifiedAt = FixedTimeProvider.Default,
        });
        _repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Update_KeepingAnInactiveCategory_IsAllowed()
    {
        _catalog.Seed(VehicleCatalogKind.TollCategory, "OLD", "Veraltet", isActive: false);
        OrderTollCharge toll = SeedToll("OLD");

        Result<OrderTollResponse> result = await UpdateHandler().Handle(
            new UpdateOrderTollCommand(_order.Id, toll.Id, "old", 1, 2, 0.1m), CancellationToken.None);

        result.Value.TollCategory.Should().Be("OLD");
    }

    [Fact]
    public async Task Update_TollOfAnotherOrder_ReturnsNotFound()
    {
        OrderTollCharge foreign = SeedToll(orderId: Guid.CreateVersion7());

        Result<OrderTollResponse> result = await UpdateHandler().Handle(
            new UpdateOrderTollCommand(_order.Id, foreign.Id, "D-015", 0, 0, 0m), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("OrderTollCharge.NotFound");
    }

    [Fact]
    public async Task Update_InvalidKilometres_ReturnsDomainErrorAndDoesNotSave()
    {
        OrderTollCharge toll = SeedToll();

        Result<OrderTollResponse> result = await UpdateHandler().Handle(
            new UpdateOrderTollCommand(_order.Id, toll.Id, "D-015", -1, 0, 0m), CancellationToken.None);

        result.Error!.Code.Should().Be("OrderTollCharge.KilometersOutOfRange");
        _repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Delete_ExistingToll_RemovesIt()
    {
        OrderTollCharge toll = SeedToll();

        Result<Unit> result = await new DeleteOrderTollCommandHandler(_repository).Handle(
            new DeleteOrderTollCommand(_order.Id, toll.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repository.Tolls.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_UnknownToll_ReturnsNotFound()
    {
        Result<Unit> result = await new DeleteOrderTollCommandHandler(_repository).Handle(
            new DeleteOrderTollCommand(_order.Id, Guid.CreateVersion7()), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task List_Tolls_ReturnsOnlyTheOrdersRecords()
    {
        SeedToll("D-015", 10, 20, 0.15m);
        SeedToll("A-0300", 5, 15, 0.3m);
        SeedToll(orderId: Guid.CreateVersion7());

        Result<OrderTollResponse[]> result = await new ListOrderTollsQueryHandler(_repository)
            .Handle(new ListOrderTollsQuery(_order.Id), CancellationToken.None);

        result.Value.Select(t => t.TollCategory).Should().Equal("D-015", "A-0300");
    }

    [Fact]
    public async Task List_UnknownOrder_ReturnsNotFound()
    {
        Result<OrderTollResponse[]> result = await new ListOrderTollsQueryHandler(_repository)
            .Handle(new ListOrderTollsQuery(Guid.CreateVersion7()), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Validator_InvalidFields_ReportsEachField()
    {
        ValidationResult result = new AddOrderTollCommandValidator().Validate(new AddOrderTollCommand(
            _order.Id, "", -1, 200_000, 1234.56789m));

        result.Errors.Select(e => e.PropertyName).Distinct().Should().BeEquivalentTo(
            ["TollCategory", "EmptyKm", "LoadedKm", "FactorPerKm"]);
    }

    [Fact]
    public void Validator_CategoryOnly_IsValid()
    {
        new AddOrderTollCommandValidator().Validate(new AddOrderTollCommand(_order.Id, "D-015", 0, 0, 0m))
            .IsValid.Should().BeTrue();
    }
}
