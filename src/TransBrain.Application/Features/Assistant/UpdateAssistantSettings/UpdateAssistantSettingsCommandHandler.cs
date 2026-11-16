using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Assistant.UpdateAssistantSettings;

internal sealed class UpdateAssistantSettingsCommandHandler(IAssistantConfigurationStore store)
    : ICommandHandler<UpdateAssistantSettingsCommand, AssistantSettingsResponse>
{
    public async Task<Result<AssistantSettingsResponse>> Handle(
        UpdateAssistantSettingsCommand command,
        CancellationToken cancellationToken)
    {
        AssistantSettingsPayload payload = command.Payload;

        AssistantConfigurationUpdate update = new(
            payload.Enabled,
            payload.Provider.Trim().ToLowerInvariant(),
            payload.Model.Trim(),
            string.IsNullOrWhiteSpace(payload.CustomBaseUrl) ? null : payload.CustomBaseUrl.Trim(),
            payload.AutonomousActionsEnabled,
            string.IsNullOrWhiteSpace(payload.ApiKey) ? null : payload.ApiKey.Trim());

        Result<AssistantConfiguration> saved = await store.SaveAsync(update, cancellationToken);
        return saved.IsSuccess
            ? AssistantSettingsResponse.From(saved.Value)
            : Result<AssistantSettingsResponse>.Failure(saved.Error!);
    }
}
