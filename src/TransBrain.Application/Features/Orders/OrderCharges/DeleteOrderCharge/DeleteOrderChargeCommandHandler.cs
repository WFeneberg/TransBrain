using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.Orders.OrderCharges.DeleteOrderCharge;

internal sealed class DeleteOrderChargeCommandHandler(ITransportOrderRepository repository)
    : ICommandHandler<DeleteOrderChargeCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteOrderChargeCommand command, CancellationToken cancellationToken)
    {
        OrderAdditionalCharge? charge = await repository.GetChargeAsync(
            command.OrderId, command.ChargeId, cancellationToken);
        if (charge is null)
        {
            return OrderChargeErrors.NotFound(command.ChargeId);
        }

        await repository.RemoveChargeAsync(charge, cancellationToken);

        return Unit.Value;
    }
}
