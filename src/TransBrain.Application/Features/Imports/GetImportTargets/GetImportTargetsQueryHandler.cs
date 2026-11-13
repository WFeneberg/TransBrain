using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Imports.Targets;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Imports.GetImportTargets;

internal sealed class GetImportTargetsQueryHandler
    : IQueryHandler<GetImportTargetsQuery, IReadOnlyList<ImportTargetResponse>>
{
    public Task<Result<IReadOnlyList<ImportTargetResponse>>> Handle(
        GetImportTargetsQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ImportTargetResponse> targets = ImportTargetRegistry.All
            .Select(t => new ImportTargetResponse(
                t.Entity.ToString(),
                t.Label,
                t.Fields
                    .Select(f => new ImportTargetFieldResponse(
                        f.Name, f.Label, f.Kind.ToString(), f.Required, f.Description, f.AllowedValues))
                    .ToList()))
            .ToList();

        return Task.FromResult(Result<IReadOnlyList<ImportTargetResponse>>.Success(targets));
    }
}
