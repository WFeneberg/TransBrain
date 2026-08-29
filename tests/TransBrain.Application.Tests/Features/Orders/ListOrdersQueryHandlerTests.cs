using AwesomeAssertions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Orders;
using TransBrain.Application.Features.Orders.ListOrders;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Tests.Features.Orders;

public class ListOrdersQueryHandlerTests
{
    private static readonly DateTimeOffset March1 = new(2027, 3, 1, 8, 0, 0, TimeSpan.Zero);

    private static TransportOrder OrderNumbered(int sequence, DateTimeOffset? pickupStart = null)
    {
        DateTimeOffset pickupFrom = pickupStart ?? March1;
        Address address = Address.Create("Absender GmbH", "Hauptstr. 1", "80331", "München", "DE").Value;

        return TransportOrder.Create(
            OrderNumber.From(2027, sequence),
            address,
            address,
            Cargo.Create("Palettenware", 12_000, 8.4m).Value,
            TimeWindow.Create(pickupFrom, pickupFrom.AddHours(2)).Value,
            TimeWindow.Create(pickupFrom.AddHours(4), pickupFrom.AddHours(8)).Value,
            pickupFrom.AddDays(-30)).Value;
    }

    [Fact]
    public async Task Handle_EmptyRepository_ReturnsEmptyPage()
    {
        ListOrdersQueryHandler handler = new(new InMemoryTransportOrderRepository());

        Result<PagedResult<OrderResponse>> result =
            await handler.Handle(new ListOrdersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_FirstPage_OrdersByOrderNumber()
    {
        InMemoryTransportOrderRepository repository = new();
        repository.Seed(OrderNumbered(30), OrderNumbered(10), OrderNumbered(20));
        ListOrdersQueryHandler handler = new(repository);

        Result<PagedResult<OrderResponse>> result =
            await handler.Handle(new ListOrdersQuery(), CancellationToken.None);

        result.Value.Items.Select(o => o.OrderNumber)
            .Should().ContainInOrder("TB-2027-00010", "TB-2027-00020", "TB-2027-00030");
    }

    [Fact]
    public async Task Handle_SecondPage_ReturnsRequestedSliceAndTotalCount()
    {
        InMemoryTransportOrderRepository repository = new();
        repository.Seed(OrderNumbered(1), OrderNumbered(2), OrderNumbered(3));
        ListOrdersQueryHandler handler = new(repository);

        Result<PagedResult<OrderResponse>> result =
            await handler.Handle(new ListOrdersQuery(Page: 2, PageSize: 2), CancellationToken.None);

        result.Value.Items.Should().ContainSingle().Which.OrderNumber.Should().Be("TB-2027-00003");
        result.Value.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_StatusFilter_ReturnsOnlyMatchingOrdersAndCountsOnlyThose()
    {
        InMemoryTransportOrderRepository repository = new();
        TransportOrder cancelled = OrderNumbered(2);
        cancelled.Cancel();
        repository.Seed(OrderNumbered(1), cancelled);
        ListOrdersQueryHandler handler = new(repository);

        Result<PagedResult<OrderResponse>> result =
            await handler.Handle(new ListOrdersQuery(Status: "Cancelled"), CancellationToken.None);

        result.Value.Items.Should().ContainSingle().Which.OrderNumber.Should().Be("TB-2027-00002");
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnknownStatusFilter_ReturnsValidationError()
    {
        ListOrdersQueryHandler handler = new(new InMemoryTransportOrderRepository());

        Result<PagedResult<OrderResponse>> result =
            await handler.Handle(new ListOrdersQuery(Status: "Sleeping"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("TransportOrder.UnknownStatus");
    }

    [Fact]
    public async Task Handle_NumericStatusFilter_ReturnsValidationError()
    {
        ListOrdersQueryHandler handler = new(new InMemoryTransportOrderRepository());

        Result<PagedResult<OrderResponse>> result =
            await handler.Handle(new ListOrdersQuery(Status: "99"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("TransportOrder.UnknownStatus");
    }

    [Fact]
    public async Task Handle_PickupFromFilter_ExcludesEarlierOrders()
    {
        InMemoryTransportOrderRepository repository = new();
        repository.Seed(OrderNumbered(1, March1), OrderNumbered(2, March1.AddDays(10)));
        ListOrdersQueryHandler handler = new(repository);

        Result<PagedResult<OrderResponse>> result = await handler.Handle(
            new ListOrdersQuery(PickupFrom: March1.AddDays(5)), CancellationToken.None);

        result.Value.Items.Should().ContainSingle().Which.OrderNumber.Should().Be("TB-2027-00002");
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_PickupToFilter_ExcludesLaterOrders()
    {
        InMemoryTransportOrderRepository repository = new();
        repository.Seed(OrderNumbered(1, March1), OrderNumbered(2, March1.AddDays(10)));
        ListOrdersQueryHandler handler = new(repository);

        Result<PagedResult<OrderResponse>> result = await handler.Handle(
            new ListOrdersQuery(PickupTo: March1.AddDays(5)), CancellationToken.None);

        result.Value.Items.Should().ContainSingle().Which.OrderNumber.Should().Be("TB-2027-00001");
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_BothPickupFilters_ReturnsOnlyOrdersInTheWindow()
    {
        InMemoryTransportOrderRepository repository = new();
        repository.Seed(
            OrderNumbered(1, March1),
            OrderNumbered(2, March1.AddDays(10)),
            OrderNumbered(3, March1.AddDays(20)));
        ListOrdersQueryHandler handler = new(repository);

        Result<PagedResult<OrderResponse>> result = await handler.Handle(
            new ListOrdersQuery(PickupFrom: March1.AddDays(5), PickupTo: March1.AddDays(15)),
            CancellationToken.None);

        result.Value.Items.Should().ContainSingle().Which.OrderNumber.Should().Be("TB-2027-00002");
        result.Value.TotalCount.Should().Be(1);
    }
}
