using AwesomeAssertions;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Features.Geodata.GetMapDataStatus;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Geodata;

public class GetMapDataStatusQueryHandlerTests
{
    [Fact]
    public async Task Handle_BothServicesUp_AnswersAvailableWithDataDates()
    {
        DateTimeOffset geocoderData = new(2026, 10, 30, 20, 21, 0, TimeSpan.Zero);
        DateTimeOffset routingData = new(2026, 10, 28, 0, 0, 0, TimeSpan.Zero);
        FakeMapDataStatusProvider provider = new()
        {
            Geocoder = new MapServiceStatus(true, geocoderData),
            Routing = new MapServiceStatus(true, routingData),
        };
        GetMapDataStatusQueryHandler handler = new(provider);

        Result<MapDataStatusResponse> result = await handler.Handle(
            new GetMapDataStatusQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Geocoder.Available.Should().BeTrue();
        result.Value.Geocoder.OsmDataUpdatedAt.Should().Be(geocoderData);
        result.Value.Routing.Available.Should().BeTrue();
        result.Value.Routing.OsmDataUpdatedAt.Should().Be(routingData);
    }

    [Fact]
    public async Task Handle_ServicesDown_IsStillASuccessfulAnswer()
    {
        GetMapDataStatusQueryHandler handler = new(new FakeMapDataStatusProvider());

        Result<MapDataStatusResponse> result = await handler.Handle(
            new GetMapDataStatusQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Geocoder.Available.Should().BeFalse();
        result.Value.Geocoder.OsmDataUpdatedAt.Should().BeNull();
        result.Value.Routing.Available.Should().BeFalse();
        result.Value.Routing.OsmDataUpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_OneServiceUp_ReportsEachIndependently()
    {
        FakeMapDataStatusProvider provider = new()
        {
            Geocoder = new MapServiceStatus(true, null),
        };
        GetMapDataStatusQueryHandler handler = new(provider);

        Result<MapDataStatusResponse> result = await handler.Handle(
            new GetMapDataStatusQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Geocoder.Available.Should().BeTrue();
        result.Value.Geocoder.OsmDataUpdatedAt.Should().BeNull();
        result.Value.Routing.Available.Should().BeFalse();
    }
}
