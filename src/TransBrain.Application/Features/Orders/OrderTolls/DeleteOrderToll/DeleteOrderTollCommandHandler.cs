using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.Orders.OrderTolls.DeleteOrderToll;

internal sealed class DeleteOrderTollCommandHandler(ITransportOrderRepository repository)
    : ICommandHandler<DeleteOrderTollCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteOrderTollCommand command, CancellationToken cancellationToken)
    {
        OrderTollCharge? toll = await repository.GetTollAsync(command.OrderId, command.TollId, cancellationToken);
        if (toll is null)
        {
            return OrderTollErrors.NotFound(command.TollId);
        }

        await repository.RemoveTollAsync(toll, cancellationToken);

        return Unit.Value;
    }
}
