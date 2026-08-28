using Microsoft.Extensions.DependencyInjection;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Common.Messaging;

internal sealed class Sender(IServiceProvider serviceProvider) : ISender
{
    public Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken)
        => Dispatch<TResponse>(command, typeof(ICommandHandler<,>), cancellationToken);

    public Task<Result<TResponse>> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken)
        => Dispatch<TResponse>(query, typeof(IQueryHandler<,>), cancellationToken);

    private Task<Result<TResponse>> Dispatch<TResponse>(
        object request,
        Type openHandlerType,
        CancellationToken cancellationToken)
    {
        Type requestType = request.GetType();
        Type handlerType = openHandlerType.MakeGenericType(requestType, typeof(TResponse));

        object? handler = serviceProvider.GetService(handlerType);
        if (handler is null)
        {
            throw new InvalidOperationException($"No handler registered for {requestType.Name}.");
        }

        RequestHandlerDelegate<TResponse> pipeline = () =>
            (Task<Result<TResponse>>)((dynamic)handler).Handle((dynamic)request, cancellationToken);

        Type behaviorType = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));
        object?[] behaviors = serviceProvider.GetServices(behaviorType).ToArray();

        for (int i = behaviors.Length - 1; i >= 0; i--)
        {
            object behavior = behaviors[i]!;
            RequestHandlerDelegate<TResponse> next = pipeline;
            pipeline = () => (Task<Result<TResponse>>)((dynamic)behavior).Handle((dynamic)request, next, cancellationToken);
        }

        return pipeline();
    }
}
