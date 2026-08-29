using AwesomeAssertions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Drivers;
using TransBrain.Application.Features.Drivers.ListDrivers;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Tests.Features.Drivers;

public class ListDriversQueryHandlerTests
{
    private static Driver DriverNamed(string firstName, string lastName) =>
        Driver.Create(firstName, lastName, [LicenseClass.C], new DateOnly(2028, 1, 1), null).Value;

    [Fact]
    public async Task Handle_EmptyRepository_ReturnsEmptyPage()
    {
        ListDriversQueryHandler handler = new(new InMemoryDriverRepository());

        Result<PagedResult<DriverResponse>> result =
            await handler.Handle(new ListDriversQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_FirstPage_OrdersByLastNameThenFirstName()
    {
        InMemoryDriverRepository repository = new();
        repository.Seed(DriverNamed("Bea", "Zimmer"), DriverNamed("Anton", "Meier"), DriverNamed("Zoe", "Meier"));
        ListDriversQueryHandler handler = new(repository);

        Result<PagedResult<DriverResponse>> result =
            await handler.Handle(new ListDriversQuery(), CancellationToken.None);

        result.Value.Items.Select(d => d.LastName + "," + d.FirstName)
            .Should().ContainInOrder("Meier,Anton", "Meier,Zoe", "Zimmer,Bea");
    }

    [Fact]
    public async Task Handle_SecondPage_ReturnsRequestedSliceAndTotalCount()
    {
        InMemoryDriverRepository repository = new();
        repository.Seed(DriverNamed("A", "Aa"), DriverNamed("B", "Bb"), DriverNamed("C", "Cc"));
        ListDriversQueryHandler handler = new(repository);

        Result<PagedResult<DriverResponse>> result =
            await handler.Handle(new ListDriversQuery(Page: 2, PageSize: 2), CancellationToken.None);

        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].LastName.Should().Be("Cc");
        result.Value.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_StatusFilter_ReturnsOnlyMatchingDriversAndCountsOnlyThose()
    {
        InMemoryDriverRepository repository = new();
        Driver absent = DriverNamed("Abs", "Ent");
        absent.MarkAbsent();
        repository.Seed(DriverNamed("Ava", "Ilable"), absent);
        ListDriversQueryHandler handler = new(repository);

        Result<PagedResult<DriverResponse>> result =
            await handler.Handle(new ListDriversQuery(Status: "Absent"), CancellationToken.None);

        result.Value.Items.Should().ContainSingle();
        result.Value.Items[0].Status.Should().Be("Absent");
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnknownStatusFilter_ReturnsValidationError()
    {
        ListDriversQueryHandler handler = new(new InMemoryDriverRepository());

        Result<PagedResult<DriverResponse>> result =
            await handler.Handle(new ListDriversQuery(Status: "Sleeping"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.UnknownStatus");
    }

    [Fact]
    public async Task Handle_NumericStatusFilter_ReturnsValidationError()
    {
        ListDriversQueryHandler handler = new(new InMemoryDriverRepository());

        Result<PagedResult<DriverResponse>> result =
            await handler.Handle(new ListDriversQuery(Status: "99"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.UnknownStatus");
    }
}
