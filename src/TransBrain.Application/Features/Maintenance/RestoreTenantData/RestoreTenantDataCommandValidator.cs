using FluentValidation;
using TransBrain.Application.Abstractions;

namespace TransBrain.Application.Features.Maintenance.RestoreTenantData;

/// <remarks>
/// Shape checks only. Whether the file matches the server's schema and the caller's tenant
/// needs async lookups and precise error codes, so the handler owns those decisions.
/// </remarks>
public sealed class RestoreTenantDataCommandValidator : AbstractValidator<RestoreTenantDataCommand>
{
    public RestoreTenantDataCommandValidator()
    {
        RuleFor(c => c.Backup).NotNull();

        When(c => c.Backup is not null, () =>
        {
            RuleFor(c => c.Backup.FileFormat)
                .Equal(TenantBackupDocument.Format)
                .WithMessage($"The file is not a TransBrain tenant backup ('{TenantBackupDocument.Format}').");
            RuleFor(c => c.Backup.SchemaVersion).NotEmpty();
            RuleFor(c => c.Backup.TenantId).NotEmpty();
            RuleFor(c => c.Backup.Tables).NotNull();
        });
    }
}
