using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Application.Features.Vehicles.CreateVehicle;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Vehicles;

public class CreateVehicleThroughSenderTests
{
    private static CreateVehicleCommand ValidCommand => new("M-AB 1234", "Tractor", 24_000, 13.6m, new DateOnly(2027, 3, 31));

    private static ISender BuildSender(out InMemoryVehicleRepository repository)
    {
        InMemoryVehicleRepository createdRepository = new();
        repository = createdRepository;

        ServiceCollection services = new();
        services.AddApplication();
        services.AddSingleton<IVehicleRepository>(createdRepository);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Fact]
    public async Task Send_CreateVehicleWithNonPositivePayload_ReturnsDomainErrorCode()
    {
        ISender sender = BuildSender(out _);

        Result<VehicleResponse> result = await sender.Send(ValidCommand with { PayloadKg = 0 }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Vehicle.PayloadKgNotPositive");
    }

    [Fact]
    public async Task Send_CreateVehicleWithBlankLicensePlate_ReturnsDomainErrorCode()
    {
        ISender sender = BuildSender(out _);

        Result<VehicleResponse> result = await sender.Send(ValidCommand with { LicensePlate = "   " }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("LicensePlate.Empty");
    }

    [Fact]
    public async Task Send_CreateVehicleWithValidCommand_ReturnsCreatedVehicle()
    {
        ISender sender = BuildSender(out InMemoryVehicleRepository repository);

        Result<VehicleResponse> result = await sender.Send(ValidCommand, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.LicensePlate.Should().Be("M-AB 1234");
        result.Value.Status.Should().Be("Available");
        repository.Vehicles.Should().ContainSingle();
    }
}
