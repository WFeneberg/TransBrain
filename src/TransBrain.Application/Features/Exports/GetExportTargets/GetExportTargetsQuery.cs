using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Exports.GetExportTargets;

/// <summary>Lists what can be exported, with the tenant's current row counts for the UI.</summary>
public sealed record GetExportTargetsQuery : IQuery<IReadOnlyList<ExportTargetResponse>>;
