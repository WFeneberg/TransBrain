using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Maintenance.RestoreTenantData;

public sealed record RestoreTenantDataCommand(TenantBackupDocument Backup)
    : ICommand<RestoreTenantDataResponse>;
