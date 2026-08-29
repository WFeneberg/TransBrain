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
    public static OrderNumber From(int year, int sequence) => new($"TB-{year:D4}-{sequence:D5}");

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
