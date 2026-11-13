using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers.CreateDriver;

internal sealed class CreateDriverCommandHandler(
    IDriverRepository repository, IVehicleCatalogRepository catalog, ICacheService cache)
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

        Result<DriverWriteMapping.DriverWriteValues> values = await DriverWriteMapping.ToDomainAsync(
            command.SocialInsurance,
            command.Documents,
            command.ClothingSizes,
            command.Relatives,
            command.SpecialDocuments,
            command.PhoneNumbers,
            catalog,
            currentPhoneTypeCodes: [],
            currentDocumentTypeCodes: [],
            cancellationToken);
        if (!values.IsSuccess)
        {
            return values.Error!;
        }

        Result<Driver> driver = Driver.Create(
            command.FirstName,
            command.LastName,
            classes.Value,
            command.LicenseValidUntil,
            command.ExternalUserId,
            values.Value.SocialInsurance,
            values.Value.Documents,
            values.Value.ClothingSizes,
            values.Value.Relatives,
            values.Value.SpecialDocuments,
            values.Value.PhoneNumbers);

        if (!driver.IsSuccess)
        {
            return driver.Error!;
        }

        Result<Driver> added = await repository.AddAsync(driver.Value, cancellationToken);
        if (!added.IsSuccess)
        {
            return added.Error!;
        }

        await cache.RemoveByPrefixAsync(DriverCacheKeys.Prefix, cancellationToken);

        return DriverResponse.From(added.Value);
    }
}
