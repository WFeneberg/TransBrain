namespace TransBrain.Domain.Common;

public readonly record struct Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    private Result(T value)
    {
        _value = value;
        _error = null;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        _value = default;
        _error = error;
        IsSuccess = false;
    }

    public bool IsSuccess { get; }

    public Error? Error => IsSuccess
        ? null
        : _error ?? throw new InvalidOperationException(
            "Result<T> was created without a value or an error. It was most likely produced by default(Result<T>) or new Result<T>(), which bypass the private constructors.");

    public T Value
    {
        get
        {
            if (IsSuccess)
            {
                return _value!;
            }

            _ = Error; // throws the uninitialized-Result diagnosis when this instance bypassed the constructors.
            throw new InvalidOperationException("Cannot access Value of a failed Result.");
        }
    }

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(Error error) => new(error);

    // Error must never be used as T: Result<Error> would give these two operators identical signatures.
    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
