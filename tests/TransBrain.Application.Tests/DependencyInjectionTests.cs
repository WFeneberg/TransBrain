using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TransBrain.Application.Common.Behaviors;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Application.Features.Vehicles.CreateVehicle;

namespace TransBrain.Application.Tests;

public class DependencyInjectionTests
{
    public sealed record SampleCommand(string Name) : ICommand<string>;

    [Fact]
    public void AddApplication_WhenResolved_RegistersLoggingBehaviorBeforeValidationBehavior()
    {
        ServiceCollection services = new();
        services.AddApplication();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        ServiceProvider provider = services.BuildServiceProvider();

        IPipelineBehavior<SampleCommand, string>[] behaviors = provider
            .GetServices<IPipelineBehavior<SampleCommand, string>>()
            .ToArray();

        behaviors.Select(b => b.GetType()).Should().Equal(
            typeof(LoggingBehavior<SampleCommand, string>),
            typeof(ValidationBehavior<SampleCommand, string>));
    }

    [Fact]
    public void AddApplication_WhenResolved_RegistersHandlersFoundByAssemblyScan()
    {
        // CreateVehicleCommandHandler (Task 7) is the first real ICommandHandler in the
        // Application assembly, so it pins that the reflection-based scan in AddApplication
        // actually discovers and registers `internal` handler implementations.
        ServiceCollection services = new();
        services.AddApplication();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(ICommandHandler<CreateVehicleCommand, VehicleResponse>) &&
            descriptor.ImplementationType == typeof(CreateVehicleCommandHandler));

        ServiceProvider provider = services.BuildServiceProvider();

        ISender sender = provider.GetRequiredService<ISender>();

        sender.Should().NotBeNull();
    }
}
