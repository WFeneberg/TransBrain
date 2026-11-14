using FluentValidation;
using TransBrain.Application.Features.InvoiceDesigns.UpdateInvoiceDesign;

namespace TransBrain.Application.Features.InvoiceDesigns.PreviewInvoiceDesign;

/// <remarks>Same rules as saving: the preview must show exactly what could be saved.</remarks>
public sealed class PreviewInvoiceDesignQueryValidator : AbstractValidator<PreviewInvoiceDesignQuery>
{
    public PreviewInvoiceDesignQueryValidator()
    {
        RuleFor(q => q.Design).NotNull();

        When(q => q.Design is not null, () =>
            RuleFor(q => q.Design).SetValidator(new InvoiceDesignPayloadValidator()));
    }
}
