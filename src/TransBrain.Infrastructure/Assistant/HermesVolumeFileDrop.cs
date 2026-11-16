using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;

namespace TransBrain.Infrastructure.Assistant;

/// <summary>
/// Writes chat attachments into the agent's data volume under <c>uploads/</c>. The Hermes API has
/// no file upload, but the volume is shared: the agent reads the dropped files with its own file
/// tools at the path this class reports. Uploads are transient working files — anything older
/// than a day is pruned on the next drop, and the volume's whitelist .gitignore keeps them out
/// of git.
/// </summary>
public sealed class HermesVolumeFileDrop(
    IOptions<HermesAgentOptions> options,
    ILogger<HermesVolumeFileDrop> logger) : IAssistantFileDrop
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    public async Task<Result<IReadOnlyList<AssistantDroppedFile>>> SaveAsync(
        IReadOnlyList<AssistantFileToDrop> files,
        CancellationToken cancellationToken)
    {
        string? dataDirectory = options.Value.DataDirectory;
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            return Error.Unavailable(
                "Assistant.NotProvisioned",
                "No AI assistant is provisioned on this installation.");
        }

        string uploadsRoot = Path.Combine(dataDirectory, "uploads");
        string batchName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..22];
        string batchDirectory = Path.Combine(uploadsRoot, batchName);

        List<AssistantDroppedFile> dropped = new(files.Count);
        try
        {
            Directory.CreateDirectory(batchDirectory);

            HashSet<string> taken = new(StringComparer.OrdinalIgnoreCase);
            foreach (AssistantFileToDrop file in files)
            {
                string name = UniqueName(SanitizeFileName(file.FileName), taken);
                await File.WriteAllBytesAsync(Path.Combine(batchDirectory, name), file.Content, cancellationToken);
                dropped.Add(new AssistantDroppedFile(
                    name,
                    $"{options.Value.AgentDataPath.TrimEnd('/')}/uploads/{batchName}/{name}"));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            logger.LogWarning(exception, "Could not write chat attachments to the assistant volume at {Path}.", batchDirectory);
            return Error.Unavailable(
                "Assistant.VolumeNotWritable",
                "The assistant's data volume is not writable; the attachments could not be handed over.");
        }

        PruneOldUploads(uploadsRoot, batchName);
        return dropped;
    }

    /// <summary>Keeps the base name, strips any path, and leaves only filesystem-safe characters.</summary>
    private static string SanitizeFileName(string fileName)
    {
        string name = fileName.Trim();
        // Treat both separators as path cuts regardless of host OS — client names are untrusted.
        int cut = name.LastIndexOfAny(['/', '\\']);
        if (cut >= 0)
        {
            name = name[(cut + 1)..];
        }
        name = Path.GetFileName(name);
        char[] cleaned = name.Select(c =>
            char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or ' ' ? c : '_').ToArray();
        string result = new string(cleaned).Trim(' ', '.');
        if (result.Length > 100)
        {
            result = result[^100..].TrimStart(' ', '.');
        }
        return result.Length > 0 ? result : "datei";
    }

    private static string UniqueName(string name, HashSet<string> taken)
    {
        string candidate = name;
        for (int i = 2; !taken.Add(candidate); i++)
        {
            candidate = $"{Path.GetFileNameWithoutExtension(name)}_{i}{Path.GetExtension(name)}";
        }
        return candidate;
    }

    /// <summary>Best effort: a failed cleanup must never fail the chat turn.</summary>
    private void PruneOldUploads(string uploadsRoot, string currentBatch)
    {
        try
        {
            foreach (string directory in Directory.EnumerateDirectories(uploadsRoot))
            {
                if (Path.GetFileName(directory) != currentBatch
                    && Directory.GetLastWriteTimeUtc(directory) < DateTime.UtcNow - Retention)
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(exception, "Could not prune old assistant uploads under {Path}.", uploadsRoot);
        }
    }
}
