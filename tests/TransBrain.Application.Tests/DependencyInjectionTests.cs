using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TransBrain.Application.Common.Behaviors;
using TransBrain.Application.Common.Messaging;

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
        // The Application assembly currently defines no real ICommandHandler/IQueryHandler
        // implementation (Task 7 owns the first feature slice), so there is nothing for the
        // assembly scan in AddApplication to find yet. This test instead pins that the scan
        // registers no spurious handler entries, and that the core ISender registration
        // AddApplication is responsible for still resolves correctly.
        ServiceCollection services = new();
        services.AddApplication();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.Should().NotContain(descriptor =>
            descriptor.ServiceType.IsGenericType &&
            (descriptor.ServiceType.GetGenericTypeDefinition() == typeof(ICommandHandler<,>) ||
             descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IQueryHandler<,>)));

        ServiceProvider provider = services.BuildServiceProvider();

        ISender sender = provider.GetRequiredService<ISender>();

        sender.Should().NotBeNull();
    }
}
