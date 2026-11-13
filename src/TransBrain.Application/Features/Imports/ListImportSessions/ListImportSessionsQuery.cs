using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Imports.ListImportSessions;

/// <summary>The import history, newest first.</summary>
public sealed record ListImportSessionsQuery : IQuery<IReadOnlyList<ImportSessionResponse>>
{
    public const int MaxSessions = 200;
}
