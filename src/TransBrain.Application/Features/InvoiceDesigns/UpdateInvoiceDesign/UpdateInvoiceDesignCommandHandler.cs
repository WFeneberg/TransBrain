using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.InvoiceDesigns.UpdateInvoiceDesign;

internal sealed class UpdateInvoiceDesignCommandHandler(
    IInvoiceDesignRepository repository,
    TimeProvider clock)
    : ICommandHandler<UpdateInvoiceDesignCommand, InvoiceDesignResponse>
{
    public async Task<Result<InvoiceDesignResponse>> Handle(
        UpdateInvoiceDesignCommand command,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();
        InvoiceDesign? design = await repository.FindAsync(cancellationToken);

        if (design is null)
        {
            // First save for this tenant: the row is created lazily, so tenants that never open
            // the designer never carry one and keep rendering with the deployment defaults.
            Result<InvoiceDesign> created = command.Design.ToNewDesign(now);
            if (!created.IsSuccess)
            {
                return created.Error!;
            }

            design = created.Value;
            await repository.AddAsync(design, cancellationToken);
        }
        else
        {
            Result<Unit> updated = command.Design.ApplyTo(design, now);
            if (!updated.IsSuccess)
            {
                return updated.Error!;
            }
        }

        await repository.SaveChangesAsync(cancellationToken);
        return InvoiceDesignResponse.From(design, isCustomized: true);
    }
}
