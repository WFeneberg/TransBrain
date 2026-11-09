using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers.DeleteDriver;

internal sealed class DeleteDriverCommandHandler(IDriverRepository repository, ICacheService cache)
    : ICommandHandler<DeleteDriverCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteDriverCommand command, CancellationToken cancellationToken)
    {
        Driver? driver = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (driver is null)
        {
            return Error.NotFound("Driver.NotFound", $"No driver with id '{command.Id}'.");
        }

        await repository.RemoveAsync(driver, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        await cache.RemoveByPrefixAsync(DriverCacheKeys.Prefix, cancellationToken);

        // The database clears vehicles' DefaultDriverId (ON DELETE SET NULL), so cached vehicles
        // would otherwise keep pointing at the deleted driver.
        await cache.RemoveByPrefixAsync(VehicleCacheKeys.Prefix, cancellationToken);

        return Unit.Value;
    }
}
