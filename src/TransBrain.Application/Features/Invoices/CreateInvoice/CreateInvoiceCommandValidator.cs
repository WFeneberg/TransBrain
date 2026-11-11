using FluentValidation;

namespace TransBrain.Application.Features.Invoices.CreateInvoice;

/// <remarks>
/// Mirrors — and never extends — the domain's own checks in Invoice.AddLine, except for the
/// duplicate-order rule: a payload naming the same order twice is a malformed request, and
/// reporting it per field beats the single Conflict the domain would return for the second line.
/// </remarks>
public sealed class CreateInvoiceCommandValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceCommandValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.VatRatePercent).InclusiveBetween(0m, 100m);

        // Like the AddressPayload rules in CreateOrderCommandValidator: a JSON body omitting
        // "lines" deserialises to null, and the collection rules below would throw on it.
        RuleFor(c => c.Lines).NotNull();

        When(c => c.Lines is not null, () =>
        {
            RuleFor(c => c.Lines).NotEmpty()
                .WithMessage("An invoice needs at least one order to bill.");

            RuleFor(c => c.Lines)
                .Must(lines => lines.Select(line => line.OrderId).Distinct().Count() == lines.Count)
                .WithMessage("An order can appear only once per invoice.");

            RuleForEach(c => c.Lines).ChildRules(line =>
            {
                line.RuleFor(l => l.OrderId).NotEmpty();
                line.RuleFor(l => l.NetAmount).GreaterThan(0m);
            });
        });
    }
}
