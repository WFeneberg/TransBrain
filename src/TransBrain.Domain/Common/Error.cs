namespace TransBrain.Domain.Common;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
    Unavailable
}

public sealed record Error(string Code, string Message, ErrorType Type)
{
    /// <summary>
    /// Per-field validation messages, keyed by field name. Populated by <c>ValidationBehavior</c>
    /// from validator failures and by lookups that reject one specific write field (for example an
    /// unknown vehicle catalog code). A domain invariant produces a
    /// coded error with no field to attach to, so this stays null there — and the API
    /// must not pretend otherwise by inventing a field key from the code.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? Failures { get; init; }

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error ValidationFailures(IReadOnlyDictionary<string, string[]> failures) =>
        new("Validation.Failed", "One or more fields are invalid.", ErrorType.Validation)
        {
            Failures = failures
        };

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    /// <summary>An external dependency could not answer. Distinct from a Conflict: retrying later may succeed.</summary>
    public static Error Unavailable(string code, string message) => new(code, message, ErrorType.Unavailable);
}
