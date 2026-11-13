using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Maintenance.BackupTenantData;

public sealed record BackupTenantDataQuery : IQuery<TenantBackupDocument>;
