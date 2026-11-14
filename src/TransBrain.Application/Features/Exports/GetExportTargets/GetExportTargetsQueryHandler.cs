using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Imports;
using TransBrain.Application.Features.Imports.Targets;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Exports.GetExportTargets;

internal sealed class GetExportTargetsQueryHandler(
    ICustomerRepository customers,
    IDriverRepository drivers,
    IVehicleRepository vehicles)
    : IQueryHandler<GetExportTargetsQuery, IReadOnlyList<ExportTargetResponse>>
{
    public async Task<Result<IReadOnlyList<ExportTargetResponse>>> Handle(
        GetExportTargetsQuery query, CancellationToken cancellationToken)
    {
        var targets = new List<ExportTargetResponse>(ImportTargetRegistry.All.Count);
        foreach (IImportTargetDefinition target in ImportTargetRegistry.All)
        {
            int count = target.Entity switch
            {
                ImportTargetEntity.Customers => await customers.CountAsync(cancellationToken),
                ImportTargetEntity.Drivers => await drivers.CountAsync(null, cancellationToken),
                ImportTargetEntity.Vehicles => await vehicles.CountAsync(null, null, cancellationToken),
                _ => 0
            };

            targets.Add(new ExportTargetResponse(target.Entity.ToString(), target.Label, count));
        }

        return targets;
    }
}
