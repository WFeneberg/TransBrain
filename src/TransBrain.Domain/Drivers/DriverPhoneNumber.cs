using TransBrain.Domain.Common;

namespace TransBrain.Domain.Drivers;

/// <summary>
/// A phone number of a driver ("Telefone"), with its kind held as a code of the maintainable
/// phone-type catalog (see <c>VehicleCatalogKind.PhoneType</c>); the application layer resolves it.
/// The <see cref="Sequence"/> keeps the order the user entered them in; it is assigned by
/// <see cref="Driver"/> and starts at 1.
/// </summary>
public sealed record DriverPhoneNumber
{
    public const int MaxTypeLength = 50;
    public const int MaxNumberLength = 50;
    public const int MaxCommentLength = 200;

    private DriverPhoneNumber(int sequence, string number, string? type, string? comment)
    {
        Sequence = sequence;
        Number = number;
        Type = type;
        Comment = comment;
    }

    public int Sequence { get; }

    /// <summary>The phone number itself.</summary>
    public string Number { get; }

    /// <summary>Kind of number as phone-type catalog code; the domain itself only bounds its length.</summary>
    public string? Type { get; }

    /// <summary>Free-text remark.</summary>
    public string? Comment { get; }

    public static Result<DriverPhoneNumber> Create(string number, string? type = null, string? comment = null)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            return Error.Validation(
                "DriverPhoneNumber.NumberRequired", "The phone number must not be empty.");
        }

        string trimmedNumber = number.Trim();
        string? normalizedType = OptionalText.Normalize(type);
        string? normalizedComment = OptionalText.Normalize(comment);

        if (trimmedNumber.Length > MaxNumberLength
            || normalizedType is { Length: > MaxTypeLength }
            || normalizedComment is { Length: > MaxCommentLength })
        {
            return Error.Validation(
                "DriverPhoneNumber.TextTooLong",
                $"A phone number must not exceed {MaxNumberLength} characters, its kind "
                + $"{MaxTypeLength}, the remark {MaxCommentLength}.");
        }

        return new DriverPhoneNumber(0, trimmedNumber, normalizedType, normalizedComment);
    }

    internal DriverPhoneNumber WithSequence(int sequence) => new(sequence, Number, Type, Comment);
}
