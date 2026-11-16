using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;

namespace TransBrain.Infrastructure.Assistant;

/// <summary>
/// Reads and writes the Hermes Agent's <c>.env</c> inside its data volume — deliberately no
/// database: the volume is the single source of truth the user asked for, survives both
/// containers, and Hermes re-reads it at the start of every turn, so changes made in the central
/// administration apply without a restart. Lines this store does not manage (keys a user added by
/// hand, comments) are preserved verbatim.
/// </summary>
public sealed class HermesEnvAssistantConfigurationStore(
    IOptions<HermesAgentOptions> options,
    ILogger<HermesEnvAssistantConfigurationStore> logger) : IAssistantConfigurationStore
{
    private const string EnabledKey = "TRANSBRAIN_ASSISTANT_ENABLED";
    private const string ProviderKey = "TRANSBRAIN_ASSISTANT_PROVIDER";
    private const string ModelKey = "TRANSBRAIN_ASSISTANT_MODEL";
    private const string AutonomyKey = "TRANSBRAIN_ASSISTANT_AUTONOMY";
    private const string CustomBaseUrlKey = "OPENAI_BASE_URL";

    private const string DefaultProvider = "openrouter";

    /// <summary>UI provider id → environment variable carrying that provider's API key.</summary>
    private static readonly Dictionary<string, string> ApiKeyVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        ["openrouter"] = "OPENROUTER_API_KEY",
        ["openai-api"] = "OPENAI_API_KEY",
        ["anthropic"] = "ANTHROPIC_API_KEY",
        ["gemini"] = "GOOGLE_API_KEY",
        ["deepseek"] = "DEEPSEEK_API_KEY",
        ["xai"] = "XAI_API_KEY",
        ["custom"] = "OPENAI_API_KEY"
    };

    // One writer at a time per process; the write itself is atomic (temp file + move) so the
    // Hermes container never observes a torn file through the bind mount.
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    public async Task<AssistantConfiguration> LoadAsync(CancellationToken cancellationToken)
    {
        string? envPath = EnvFilePath();
        if (envPath is null)
        {
            return NotProvisioned();
        }

        Dictionary<string, string> values;
        try
        {
            values = File.Exists(envPath)
                ? Parse(await File.ReadAllLinesAsync(envPath, cancellationToken))
                : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not read the assistant volume at {Path}.", envPath);
            return NotProvisioned();
        }

        string provider = Normalize(values.GetValueOrDefault(ProviderKey)) ?? DefaultProvider;
        bool hasKey = ApiKeyVariables.TryGetValue(provider, out string? keyVariable)
                      && !string.IsNullOrWhiteSpace(values.GetValueOrDefault(keyVariable));

        return new AssistantConfiguration(
            Provisioned: true,
            Enabled: IsTrue(values.GetValueOrDefault(EnabledKey)),
            Provider: provider,
            Model: values.GetValueOrDefault(ModelKey, string.Empty),
            CustomBaseUrl: Normalize(values.GetValueOrDefault(CustomBaseUrlKey)),
            AutonomousActionsEnabled: IsTrue(values.GetValueOrDefault(AutonomyKey)),
            HasApiKey: hasKey);
    }

    public async Task<Result<AssistantConfiguration>> SaveAsync(
        AssistantConfigurationUpdate update, CancellationToken cancellationToken)
    {
        string? envPath = EnvFilePath();
        if (envPath is null)
        {
            return Error.Unavailable(
                "Assistant.NotProvisioned",
                "No AI assistant is provisioned on this installation.");
        }

        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            List<string> lines;
            try
            {
                lines = File.Exists(envPath)
                    ? [.. await File.ReadAllLinesAsync(envPath, cancellationToken)]
                    : [];
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogError(exception, "Could not read the assistant volume at {Path}.", envPath);
                return VolumeNotWritable();
            }

            string provider = update.Provider.ToLowerInvariant();

            Upsert(lines, EnabledKey, update.Enabled ? "true" : "false");
            Upsert(lines, ProviderKey, provider);
            Upsert(lines, ModelKey, update.Model);
            Upsert(lines, AutonomyKey, update.AutonomousActionsEnabled ? "true" : "false");

            // OPENAI_BASE_URL redirects Hermes' openai-api provider. It must only exist while a
            // custom endpoint is selected; a stale value would silently hijack real OpenAI calls.
            if (string.Equals(provider, "custom", StringComparison.OrdinalIgnoreCase)
                && update.CustomBaseUrl is not null)
            {
                Upsert(lines, CustomBaseUrlKey, update.CustomBaseUrl);
            }
            else
            {
                Remove(lines, CustomBaseUrlKey);
            }

            // A null ApiKey means "keep the stored key" — the admin UI never gets it back and so
            // cannot send it back; an admin replaces a key, never edits it.
            if (!string.IsNullOrWhiteSpace(update.ApiKey)
                && ApiKeyVariables.TryGetValue(provider, out string? keyVariable))
            {
                Upsert(lines, keyVariable, update.ApiKey);
            }

            try
            {
                string tempPath = envPath + ".tmp";
                await File.WriteAllLinesAsync(tempPath, lines, cancellationToken);
                File.Move(tempPath, envPath, overwrite: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogError(exception, "Could not write the assistant volume at {Path}.", envPath);
                return VolumeNotWritable();
            }
        }
        finally
        {
            WriteLock.Release();
        }

        return await LoadAsync(cancellationToken);
    }

    private string? EnvFilePath()
    {
        string? directory = options.Value.DataDirectory;
        return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
            ? Path.Combine(directory, ".env")
            : null;
    }

    private static AssistantConfiguration NotProvisioned() => new(
        Provisioned: false,
        Enabled: false,
        Provider: DefaultProvider,
        Model: string.Empty,
        CustomBaseUrl: null,
        AutonomousActionsEnabled: false,
        HasApiKey: false);

    private static Error VolumeNotWritable() => Error.Unavailable(
        "Assistant.VolumeNotWritable",
        "The assistant's data volume could not be written. Check the mount and its permissions.");

    private static Dictionary<string, string> Parse(IEnumerable<string> lines)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            int separator = line.IndexOf('=');
            if (line.Length == 0 || line.StartsWith('#') || separator <= 0)
            {
                continue;
            }

            values[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }

        return values;
    }

    private static void Upsert(List<string> lines, string key, string value)
    {
        string entry = $"{key}={value}";
        int index = IndexOf(lines, key);
        if (index >= 0)
        {
            lines[index] = entry;
        }
        else
        {
            lines.Add(entry);
        }
    }

    private static void Remove(List<string> lines, string key)
    {
        int index = IndexOf(lines, key);
        if (index >= 0)
        {
            lines.RemoveAt(index);
        }
    }

    private static int IndexOf(List<string> lines, string key) =>
        lines.FindIndex(line =>
        {
            string trimmed = line.TrimStart();
            return !trimmed.StartsWith('#')
                   && trimmed.Length > key.Length
                   && trimmed.StartsWith(key, StringComparison.OrdinalIgnoreCase)
                   && trimmed[key.Length..].TrimStart().StartsWith('=');
        });

    private static bool IsTrue(string? value) =>
        value is not null && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
