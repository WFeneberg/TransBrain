using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<Result<TResponse>> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        string requestName = typeof(TRequest).Name;
        long start = Stopwatch.GetTimestamp();

        Result<TResponse> result = await next();

        TimeSpan elapsed = Stopwatch.GetElapsedTime(start);

        if (result.IsSuccess)
        {
            logger.LogInformation("{Request} succeeded in {ElapsedMs} ms", requestName, elapsed.TotalMilliseconds);
        }
        else
        {
            logger.LogWarning(
                "{Request} failed in {ElapsedMs} ms with {ErrorCode} ({ErrorType})",
                requestName,
                elapsed.TotalMilliseconds,
                result.Error!.Code,
                result.Error.Type);
        }

        return result;
    }
}
