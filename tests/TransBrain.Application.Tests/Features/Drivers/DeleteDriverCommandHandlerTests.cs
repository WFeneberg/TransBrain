using AwesomeAssertions;
using TransBrain.Application.Features.Drivers.DeleteDriver;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Tests.Features.Drivers;

public class DeleteDriverCommandHandlerTests
{
    [Fact]
    public async Task Handle_KnownDriver_RemovesItAndSaves()
    {
        InMemoryDriverRepository repository = new();
        Driver driver = Driver.Create("Frank", "Fahrer", [LicenseClass.C], new DateOnly(2028, 1, 1), null).Value;
        repository.Seed(driver);
        DeleteDriverCommandHandler handler = new(repository);

        Result<Unit> result = await handler.Handle(new DeleteDriverCommand(driver.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Drivers.Should().BeEmpty();
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnknownDriver_ReturnsNotFoundAndDoesNotSave()
    {
        InMemoryDriverRepository repository = new();
        DeleteDriverCommandHandler handler = new(repository);

        Result<Unit> result = await handler.Handle(
            new DeleteDriverCommand(Guid.CreateVersion7()), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
        repository.SaveChangesCallCount.Should().Be(0);
    }
}
