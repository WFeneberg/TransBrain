using TransBrain.Domain.Common;

namespace TransBrain.Application.Common.Messaging;

public delegate Task<Result<TResponse>> RequestHandlerDelegate<TResponse>();

public interface IPipelineBehavior<in TRequest, TResponse>
{
    Task<Result<TResponse>> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
