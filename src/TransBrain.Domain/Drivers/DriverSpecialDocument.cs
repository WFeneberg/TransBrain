using TransBrain.Domain.Common;

namespace TransBrain.Domain.Drivers;

/// <summary>
/// A special paper of a driver ("Sonderpapiere") such as an ADR certificate or a forklift
/// licence, with its last inspection date and expiry. The <see cref="Sequence"/> keeps the order
/// the user entered them in; it is assigned by <see cref="Driver"/> and starts at 1.
/// </summary>
public sealed record DriverSpecialDocument
{
    public const int MaxTypeLength = 200;
    public const int MaxCommentLength = 500;
    public const int MinYear = 1900;

    private DriverSpecialDocument(
        int sequence,
        string type,
        DateOnly? lastInspectionDate,
        DateOnly? validUntil,
        string? comment)
    {
        Sequence = sequence;
        Type = type;
        LastInspectionDate = lastInspectionDate;
        ValidUntil = validUntil;
        Comment = comment;
    }

    public int Sequence { get; }

    /// <summary>
    /// What paper it is: the code of a catalog entry of kind
    /// <c>VehicleCatalogKind.DriverDocumentType</c>. The application layer resolves it against the
    /// catalog; the domain only bounds its length.
    /// </summary>
    public string Type { get; }

    /// <summary>Date of the last inspection ("Letztes Prüfdatum").</summary>
    public DateOnly? LastInspectionDate { get; }

    /// <summary>Date the paper expires ("Gültig Bis").</summary>
    public DateOnly? ValidUntil { get; }

    /// <summary>Free-text remark ("Bemerkung").</summary>
    public string? Comment { get; }

    public static Result<DriverSpecialDocument> Create(
        string type,
        DateOnly? lastInspectionDate = null,
        DateOnly? validUntil = null,
        string? comment = null)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return Error.Validation(
                "DriverSpecialDocument.TypeRequired", "The special paper's type must not be empty.");
        }

        if (lastInspectionDate is { Year: < MinYear } || validUntil is { Year: < MinYear })
        {
            return Error.Validation(
                "DriverSpecialDocument.DateTooEarly",
                $"A special paper's dates must not be before {MinYear}.");
        }

        string trimmedType = type.Trim();
        string? normalizedComment = OptionalText.Normalize(comment);

        if (trimmedType.Length > MaxTypeLength || normalizedComment is { Length: > MaxCommentLength })
        {
            return Error.Validation(
                "DriverSpecialDocument.TextTooLong",
                $"A special paper's type must not exceed {MaxTypeLength} characters, "
                + $"the remark {MaxCommentLength}.");
        }

        return new DriverSpecialDocument(0, trimmedType, lastInspectionDate, validUntil, normalizedComment);
    }

    internal DriverSpecialDocument WithSequence(int sequence) =>
        new(sequence, Type, LastInspectionDate, ValidUntil, Comment);
}
