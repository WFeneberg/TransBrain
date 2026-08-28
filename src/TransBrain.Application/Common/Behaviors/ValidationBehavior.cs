using FluentValidation;
using FluentValidation.Results;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<Result<TResponse>> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        IValidator<TRequest>[] applicable = validators.ToArray();
        if (applicable.Length == 0)
        {
            return await next();
        }

        ValidationContext<TRequest> context = new(request);
        ValidationFailure[] failures = (await Task.WhenAll(
                applicable.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToArray();

        if (failures.Length == 0)
        {
            return await next();
        }

        ValidationFailure first = failures[0];
        return Error.Validation(first.PropertyName, first.ErrorMessage);
    }
}
