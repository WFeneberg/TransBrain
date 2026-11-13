using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Imports.GetImportTargets;

/// <summary>The entities data can be imported into, with their mappable fields — the mapping UI's vocabulary.</summary>
public sealed record GetImportTargetsQuery : IQuery<IReadOnlyList<ImportTargetResponse>>;

public sealed record ImportTargetFieldResponse(
    string Name,
    string Label,
    string Kind,
    bool Required,
    string? Description,
    IReadOnlyList<string>? AllowedValues);

public sealed record ImportTargetResponse(
    string Entity,
    string Label,
    IReadOnlyList<ImportTargetFieldResponse> Fields);
