using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers.CreateDriver;

internal sealed class CreateDriverCommandHandler(IDriverRepository repository)
    : ICommandHandler<CreateDriverCommand, DriverResponse>
{
    public async Task<Result<DriverResponse>> Handle(
        CreateDriverCommand command,
        CancellationToken cancellationToken)
    {
        Result<LicenseClass[]> classes = LicenseClassParser.Parse(command.LicenseClasses);
        if (!classes.IsSuccess)
        {
            return classes.Error!;
        }

        Result<Driver> driver = Driver.Create(
            command.FirstName,
            command.LastName,
            classes.Value,
            command.LicenseValidUntil,
            command.ExternalUserId);

        if (!driver.IsSuccess)
        {
            return driver.Error!;
        }

        Result<Driver> added = await repository.AddAsync(driver.Value, cancellationToken);
        if (!added.IsSuccess)
        {
            return added.Error!;
        }

        return DriverResponse.From(added.Value);
    }
}
