using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Maintenance.BackupTenantData;

internal sealed class BackupTenantDataQueryHandler(ITenantDataMaintenance maintenance)
    : IQueryHandler<BackupTenantDataQuery, TenantBackupDocument>
{
    public async Task<Result<TenantBackupDocument>> Handle(
        BackupTenantDataQuery query, CancellationToken cancellationToken)
        => await maintenance.ExportAsync(cancellationToken);
}
