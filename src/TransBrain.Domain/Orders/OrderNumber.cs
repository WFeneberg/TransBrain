using System.Text.RegularExpressions;
using TransBrain.Domain.Common;

namespace TransBrain.Domain.Orders;

public sealed partial record OrderNumber
{
    private OrderNumber(string value) => Value = value;

    public string Value { get; }

    /// <summary>
    /// Formats a number as <c>TB-{year}-{sequence:D5}</c>. The sequence is padded to five
    /// digits but NOT truncated: a haulier that exceeds 99,999 orders in a year gets a longer
    /// number rather than a duplicate one.
    /// </summary>
    /// <remarks>
    /// Throws rather than returning a <see cref="Result{T}"/>, even though the project's rule
    /// is that business failures never throw: a caller passing a four-digit-violating year or a
    /// non-positive sequence is not a user, it is the order-number generator misbehaving — a
    /// programming error, not a business failure. Guarding here matters beyond the immediate
    /// call: Task 4's EF value converter reads a stored value back through
    /// <c>Parse(value).Value</c>, so a string this factory produced but <see cref="Parse"/>
    /// would reject would blow up on materialisation, far from the code that created it, instead
    /// of failing here where the mistake was made.
    /// </remarks>
    public static OrderNumber From(int year, int sequence)
    {
        if (year is < 1000 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(year), year, "Year must be exactly four digits (1000-9999).");
        }

        if (sequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Sequence must be a positive number.");
        }

        return new($"TB-{year:D4}-{sequence:D5}");
    }

    public static Result<OrderNumber> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !OrderNumberPattern().IsMatch(value))
        {
            return Error.Validation(
                "OrderNumber.Malformed",
                "Order number must look like 'TB-2027-00042'.");
        }

        return new OrderNumber(value);
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^TB-\d{4}-\d{5,}$")]
    private static partial Regex OrderNumberPattern();
}
