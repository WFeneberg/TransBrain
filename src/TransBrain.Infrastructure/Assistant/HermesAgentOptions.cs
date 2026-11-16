namespace TransBrain.Infrastructure.Assistant;

/// <summary>
/// Wiring to the Hermes Agent container. All three values come from the orchestrator (AppHost or
/// docker-compose); when they are absent the deployment simply has no assistant and the feature
/// reports itself as not provisioned instead of failing.
/// </summary>
public sealed class HermesAgentOptions
{
    public const string SectionName = "HermesAgent";

    /// <summary>Base URL of the gateway's OpenAI-compatible API server, e.g. http://hermes:8642.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Bearer key of the Hermes API server (its API_SERVER_KEY).</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The agent's data directory (the /opt/data bind mount) as seen by THIS process. The
    /// central administration writes the agent's .env here; Hermes re-reads it every turn.
    /// </summary>
    public string? DataDirectory { get; set; }

    /// <summary>Agent turns run tools and can take a while; default generous.</summary>
    public int TimeoutSeconds { get; set; } = 300;
}
