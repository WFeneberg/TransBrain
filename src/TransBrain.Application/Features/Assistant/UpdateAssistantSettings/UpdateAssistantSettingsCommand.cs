using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Assistant.UpdateAssistantSettings;

public sealed record UpdateAssistantSettingsCommand(AssistantSettingsPayload Payload)
    : ICommand<AssistantSettingsResponse>;
