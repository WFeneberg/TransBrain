using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Assistant.ChatWithAssistant;

public sealed record ChatWithAssistantCommand(AssistantChatPayload Payload)
    : ICommand<AssistantChatResponse>;
