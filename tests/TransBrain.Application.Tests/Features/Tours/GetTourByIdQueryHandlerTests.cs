using AwesomeAssertions;
using TransBrain.Application.Features.Tours;
using TransBrain.Application.Features.Tours.GetTourById;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Tours;

public class GetTourByIdQueryHandlerTests
{
    private static GetTourByIdQueryHandler Handler(TourFixture f, StubCurrentUser? caller = null) =>
        new(f.Tours, f.Vehicles, f.Drivers, f.Orders, caller ?? StubCurrentUser.Dispatcher());

    [Fact]
    public async Task Handle_KnownId_ReturnsTour()
    {
        TourFixture f = TourFixture.Create();
        f.AssignedOrder();

        Result<TourResponse> result = await Handler(f).Handle(
            new GetTourByIdQuery(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(f.Tour.Id);
        result.Value.Stops.Should().HaveCount(2);
        result.Value.VehicleLicensePlate.Should().Be(f.Vehicle.LicensePlate.Value);
    }

    [Fact]
    public async Task Handle_UnknownId_ReturnsNotFound()
    {
        TourFixture f = TourFixture.Create();

        Result<TourResponse> result = await Handler(f).Handle(
            new GetTourByIdQuery(Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Tour.NotFound");
    }

    [Fact]
    public async Task Handle_AsTheAssignedDriver_ReturnsTour()
    {
        TourFixture f = TourFixture.Create(driverExternalUserId: "driver-sub");

        Result<TourResponse> result = await Handler(f, StubCurrentUser.DriverWith("driver-sub"))
            .Handle(new GetTourByIdQuery(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(f.Tour.Id);
    }

    // Unlike the list, which narrows, a single-tour read refuses: the caller asked for one
    // specific tour, and silently answering about a different one would be worse.
    [Fact]
    public async Task Handle_AsADifferentDriver_ReturnsForbidden()
    {
        TourFixture f = TourFixture.Create(driverExternalUserId: "driver-sub");

        Result<TourResponse> result = await Handler(f, StubCurrentUser.DriverWith("someone-else"))
            .Handle(new GetTourByIdQuery(f.Tour.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Code.Should().Be("Tour.NotYours");
    }
}
