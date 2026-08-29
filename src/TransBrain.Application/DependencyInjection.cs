using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TransBrain.Application.Common.Behaviors;
using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        Assembly assembly = typeof(DependencyInjection).Assembly;

        services.AddScoped<ISender, Sender>();
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.TryAddSingleton(TimeProvider.System);

        Type[] handlerInterfaces = [typeof(ICommandHandler<,>), typeof(IQueryHandler<,>)];

        foreach (Type implementation in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            foreach (Type service in implementation.GetInterfaces()
                         .Where(i => i.IsGenericType && handlerInterfaces.Contains(i.GetGenericTypeDefinition())))
            {
                services.AddScoped(service, implementation);
            }
        }

        return services;
    }
}
