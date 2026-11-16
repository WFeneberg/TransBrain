using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Fakes;

/// <summary>Pretends to drop files into the agent volume and records what was saved.</summary>
public sealed class FakeAssistantFileDrop : IAssistantFileDrop
{
    /// <summary>Set to make every call answer this error, as an unwritable volume would.</summary>
    public Error? Failure { get; set; }

    public List<AssistantFileToDrop> SavedFiles { get; } = [];

    public Task<Result<IReadOnlyList<AssistantDroppedFile>>> SaveAsync(
        IReadOnlyList<AssistantFileToDrop> files,
        CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            return Task.FromResult(Result<IReadOnlyList<AssistantDroppedFile>>.Failure(Failure));
        }

        SavedFiles.AddRange(files);
        IReadOnlyList<AssistantDroppedFile> dropped = files
            .Select(f => new AssistantDroppedFile(f.FileName, $"/opt/data/uploads/test/{f.FileName}"))
            .ToList();
        return Task.FromResult(Result<IReadOnlyList<AssistantDroppedFile>>.Success(dropped));
    }
}
