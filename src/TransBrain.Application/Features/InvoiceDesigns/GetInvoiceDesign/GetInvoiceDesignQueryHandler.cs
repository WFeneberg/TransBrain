using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.InvoiceDesigns.GetInvoiceDesign;

internal sealed class GetInvoiceDesignQueryHandler(IInvoiceDesignRepository repository)
    : IQueryHandler<GetInvoiceDesignQuery, InvoiceDesignResponse>
{
    public async Task<Result<InvoiceDesignResponse>> Handle(
        GetInvoiceDesignQuery query,
        CancellationToken cancellationToken)
    {
        InvoiceDesign? saved = await repository.FindAsync(cancellationToken);
        if (saved is not null)
        {
            return InvoiceDesignResponse.From(saved, isCustomized: true);
        }

        // No row yet: the designer opens pre-filled with the deployment defaults, so the first
        // save starts from a working document instead of an empty form.
        InvoiceDesign effective = await repository.GetEffectiveAsync(cancellationToken);
        return InvoiceDesignResponse.From(effective, isCustomized: false);
    }
}
