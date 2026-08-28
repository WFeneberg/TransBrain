using TransBrain.Domain.Common;

namespace TransBrain.Api.Common;

public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            return onSuccess is null ? Results.Ok(result.Value) : onSuccess(result.Value);
        }

        Error error = result.Error!;

        return error.Type switch
        {
            ErrorType.Validation => Results.ValidationProblem(
                new Dictionary<string, string[]> { [error.Code] = [error.Message] },
                title: "Validation failed"),
            ErrorType.NotFound => Results.Problem(title: error.Code, detail: error.Message, statusCode: 404),
            ErrorType.Conflict => Results.Problem(title: error.Code, detail: error.Message, statusCode: 409),
            ErrorType.Forbidden => Results.Problem(title: error.Code, detail: error.Message, statusCode: 403),
            _ => Results.Problem(title: error.Code, detail: error.Message, statusCode: 500)
        };
    }
}
