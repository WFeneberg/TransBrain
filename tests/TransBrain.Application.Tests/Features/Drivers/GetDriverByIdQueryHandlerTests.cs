using AwesomeAssertions;
using TransBrain.Application.Features.Drivers;
using TransBrain.Application.Features.Drivers.GetDriverById;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Tests.Features.Drivers;

public class GetDriverByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_KnownId_ReturnsDriver()
    {
        InMemoryDriverRepository repository = new();
        Driver driver = Driver.Create("Frank", "Fahrer", [LicenseClass.C], new DateOnly(2028, 1, 1), null).Value;
        repository.Seed(driver);
        GetDriverByIdQueryHandler handler = new(repository);

        Result<DriverResponse> result = await handler.Handle(
            new GetDriverByIdQuery(driver.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(driver.Id);
    }

    [Fact]
    public async Task Handle_UnknownId_ReturnsNotFound()
    {
        GetDriverByIdQueryHandler handler = new(new InMemoryDriverRepository());

        Result<DriverResponse> result = await handler.Handle(
            new GetDriverByIdQuery(Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Driver.NotFound");
    }
}
