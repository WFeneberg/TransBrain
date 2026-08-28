using TransBrain.Domain.Common;

namespace TransBrain.Application.Common.Messaging;

public interface ISender
{
    Task<Result<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken);

    Task<Result<TResponse>> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken);
}
