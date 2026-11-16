using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;

namespace TransBrain.Infrastructure.Assistant;

/// <summary>
/// Talks to the Hermes Agent gateway's OpenAI-compatible API server
/// (<c>POST /v1/chat/completions</c>, non-streaming). Every failure maps to
/// <see cref="ErrorType.Unavailable"/>: the assistant is an optional sidecar and must never take
/// the application down with it.
/// </summary>
public sealed class HermesAgentClient(HttpClient http, ILogger<HermesAgentClient> logger) : IAssistantGateway
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<Result<AssistantReply>> SendAsync(
        AssistantChatTurn turn, CancellationToken cancellationToken)
    {
        if (http.BaseAddress is null)
        {
            return Error.Unavailable(
                "Assistant.NotProvisioned",
                "No AI assistant is provisioned on this installation.");
        }

        ChatCompletionRequest request = new(
            string.IsNullOrWhiteSpace(turn.Model) ? "hermes-agent" : turn.Model,
            turn.Provider,
            turn.Messages.Select(m => new ChatMessage(m.Role, BuildContent(m))).ToArray());

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync(
                "v1/chat/completions", request, SerializerOptions, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                              && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Hermes gateway not reachable at {BaseAddress}.", http.BaseAddress);
            return Error.Unavailable(
                "Assistant.Unreachable",
                "The AI assistant did not answer. It may still be starting — try again shortly.");
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                logger.LogError(
                    "Hermes gateway refused the API server key (HTTP {Status}). Check HermesAgent__ApiKey against API_SERVER_KEY.",
                    (int)response.StatusCode);
                return Error.Unavailable(
                    "Assistant.AuthFailed",
                    "The AI assistant refused the server-to-server credentials. An administrator must check the gateway key.");
            }

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning(
                    "Hermes gateway answered HTTP {Status}: {Body}", (int)response.StatusCode, Truncate(body));
                return Error.Unavailable(
                    "Assistant.UpstreamError",
                    "The AI assistant could not process the request. Check the model and API key in the central administration.");
            }

            ChatCompletionResponse? completion;
            try
            {
                completion = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(
                    SerializerOptions, cancellationToken);
            }
            catch (JsonException exception)
            {
                logger.LogWarning(exception, "Hermes gateway returned a malformed chat completion.");
                return MalformedAnswer();
            }

            ChatChoice? choice = completion?.Choices is { Count: > 0 } choices ? choices[0] : null;
            if (choice?.Message?.Content is not { Length: > 0 } content)
            {
                return MalformedAnswer();
            }

            // "stop" is the normal end; "length" means the turn hit its iteration budget but the
            // text is still the agent's own wrap-up of what it found — usable, so pass it on.
            if (choice.FinishReason is "error" or "cancelled")
            {
                logger.LogWarning("Hermes turn finished with reason {Reason}.", choice.FinishReason);
                return Error.Unavailable(
                    "Assistant.UpstreamError",
                    "The AI assistant aborted the answer. Try again, or check the provider configuration.");
            }

            return new AssistantReply(content);
        }
    }

    private static Error MalformedAnswer() => Error.Unavailable(
        "Assistant.UpstreamError",
        "The AI assistant returned an unreadable answer.");

    private static string Truncate(string value) =>
        value.Length <= 500 ? value : value[..500] + "…";

    /// <summary>
    /// Plain text stays a string; messages with images become the OpenAI multimodal content array
    /// (<c>text</c> + <c>image_url</c> parts with data URLs), which the Hermes gateway supports on
    /// chat completions.
    /// </summary>
    private static object BuildContent(AssistantTurnMessage message)
    {
        if (message.Images is not { Count: > 0 } images)
        {
            return message.Content;
        }

        List<object> parts = new(images.Count + 1) { new TextPart("text", message.Content) };
        parts.AddRange(images.Select(object (image) => new ImagePart(
            "image_url",
            new ImageUrlValue($"data:{image.ContentType};base64,{image.DataBase64}"))));
        return parts;
    }

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("provider")] string? Provider,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
        [property: JsonPropertyName("stream")] bool Stream = false);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] object Content);

    private sealed record TextPart(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string Text);

    private sealed record ImagePart(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("image_url")] ImageUrlValue ImageUrl);

    private sealed record ImageUrlValue(
        [property: JsonPropertyName("url")] string Url);

    private sealed record ChatCompletionResponse(
        [property: JsonPropertyName("choices")] IReadOnlyList<ChatChoice>? Choices);

    private sealed record ChatChoice(
        [property: JsonPropertyName("message")] ChatResponseMessage? Message,
        [property: JsonPropertyName("finish_reason")] string? FinishReason);

    private sealed record ChatResponseMessage(
        [property: JsonPropertyName("content")] string? Content);
}
