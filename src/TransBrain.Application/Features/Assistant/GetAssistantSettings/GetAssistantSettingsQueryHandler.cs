using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Assistant.GetAssistantSettings;

internal sealed class GetAssistantSettingsQueryHandler(IAssistantConfigurationStore store)
    : IQueryHandler<GetAssistantSettingsQuery, AssistantSettingsResponse>
{
    public async Task<Result<AssistantSettingsResponse>> Handle(
        GetAssistantSettingsQuery query,
        CancellationToken cancellationToken)
    {
        AssistantConfiguration configuration = await store.LoadAsync(cancellationToken);
        return AssistantSettingsResponse.From(configuration);
    }
}
