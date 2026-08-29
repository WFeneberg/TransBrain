using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers.UpdateDriver;

internal sealed class UpdateDriverCommandHandler(IDriverRepository repository, ICacheService cache)
    : ICommandHandler<UpdateDriverCommand, DriverResponse>
{
    public async Task<Result<DriverResponse>> Handle(
        UpdateDriverCommand command,
        CancellationToken cancellationToken)
    {
        Driver? driver = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (driver is null)
        {
            return Error.NotFound("Driver.NotFound", $"No driver with id '{command.Id}'.");
        }

        Result<LicenseClass[]> classes = LicenseClassParser.Parse(command.LicenseClasses);
        if (!classes.IsSuccess)
        {
            return classes.Error!;
        }

        Result<Driver> updated = driver.Update(
            command.FirstName,
            command.LastName,
            classes.Value,
            command.LicenseValidUntil,
            command.ExternalUserId);

        if (!updated.IsSuccess)
        {
            return updated.Error!;
        }

        await repository.SaveChangesAsync(cancellationToken);

        await cache.RemoveByPrefixAsync(DriverCacheKeys.Prefix, cancellationToken);

        return DriverResponse.From(updated.Value);
    }
}
