using TransBrain.Domain.Common;

namespace TransBrain.Application.Abstractions;

/// <summary>
/// Hands non-image chat attachments over to the agent. The Hermes API takes no file uploads, but
/// the agent's data volume is shared with this process: files dropped there are readable with the
/// agent's own file tools, and the chat message tells it where they are.
/// </summary>
public interface IAssistantFileDrop
{
    /// <returns>For each file its (possibly sanitized) name and the path as the agent sees it.</returns>
    Task<Result<IReadOnlyList<AssistantDroppedFile>>> SaveAsync(
        IReadOnlyList<AssistantFileToDrop> files,
        CancellationToken cancellationToken);
}

public sealed record AssistantFileToDrop(string FileName, byte[] Content);

/// <param name="AgentPath">Absolute path inside the agent container, e.g. /opt/data/uploads/…</param>
public sealed record AssistantDroppedFile(string FileName, string AgentPath);
