using TransBrain.Application.Abstractions;

namespace TransBrain.Application.Features.Assistant;

/// <summary>
/// The LLM providers the central administration offers. The ids are Hermes provider ids —
/// <c>custom</c> is the exception: it stands for any OpenAI-compatible endpoint (Ollama, vLLM,
/// Azure OpenAI, ...) and is translated to Hermes' <c>openai-api</c> provider plus a base URL.
/// </summary>
public static class AssistantProviders
{
    public const string OpenRouter = "openrouter";
    public const string Custom = "custom";

    public static readonly IReadOnlyList<string> Known =
        [OpenRouter, "openai-api", "anthropic", "gemini", "deepseek", "xai", Custom];

    public static bool IsKnown(string? provider) =>
        provider is not null && Known.Contains(provider, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a chat request can be served right now. A custom endpoint may be keyless
    /// (a local Ollama for instance); every cloud provider needs its API key.
    /// </summary>
    public static bool IsReadyForChat(AssistantConfiguration configuration) =>
        configuration is { Provisioned: true, Enabled: true }
        && !string.IsNullOrWhiteSpace(configuration.Model)
        && (configuration.HasApiKey
            || string.Equals(configuration.Provider, Custom, StringComparison.OrdinalIgnoreCase));
}
