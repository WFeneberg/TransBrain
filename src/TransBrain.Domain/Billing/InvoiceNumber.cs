using System.Text.RegularExpressions;
using TransBrain.Domain.Common;

namespace TransBrain.Domain.Billing;

public sealed partial record InvoiceNumber
{
    private InvoiceNumber(string value) => Value = value;

    public string Value { get; }

    /// <summary>
    /// Formats a number as <c>RE-{year}-{sequence:D5}</c>. The sequence is padded to five
    /// digits but NOT truncated: a haulier that writes more than 99,999 invoices in a year gets
    /// a longer number rather than a duplicate one.
    /// </summary>
    /// <remarks>
    /// Throws rather than returning a <see cref="Result{T}"/> for the same reason
    /// <c>OrderNumber.From</c> does: a caller passing an invalid year or sequence is the
    /// invoice-number generator misbehaving — a programming error, not a business failure.
    /// </remarks>
    public static InvoiceNumber From(int year, int sequence)
    {
        if (year is < 1000 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(year), year, "Year must be exactly four digits (1000-9999).");
        }

        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Sequence must be a positive number.");
        }

        return new($"RE-{year:D4}-{sequence:D5}");
    }

    public static Result<InvoiceNumber> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !InvoiceNumberPattern().IsMatch(value))
        {
            return Error.Validation(
                "InvoiceNumber.Malformed",
                "Invoice number must look like 'RE-2027-00042'.");
        }

        return new InvoiceNumber(value);
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^RE-\d{4}-\d{5,}$")]
    private static partial Regex InvoiceNumberPattern();
}
