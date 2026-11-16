using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Assistant.GetAssistantStatus;

public sealed record GetAssistantStatusQuery : IQuery<AssistantStatusResponse>;
