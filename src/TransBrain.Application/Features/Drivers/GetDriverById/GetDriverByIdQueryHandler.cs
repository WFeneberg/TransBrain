using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers.GetDriverById;

internal sealed class GetDriverByIdQueryHandler(IDriverRepository repository)
    : IQueryHandler<GetDriverByIdQuery, DriverResponse>
{
    public async Task<Result<DriverResponse>> Handle(
        GetDriverByIdQuery query,
        CancellationToken cancellationToken)
    {
        Driver? driver = await repository.GetByIdAsync(query.Id, cancellationToken);

        if (driver is null)
        {
            return Error.NotFound("Driver.NotFound", $"No driver with id '{query.Id}'.");
        }

        return DriverResponse.From(driver);
    }
}
