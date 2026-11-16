using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Assistant.GetAssistantSettings;

public sealed record GetAssistantSettingsQuery : IQuery<AssistantSettingsResponse>;
