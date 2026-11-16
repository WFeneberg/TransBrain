using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Assistant.GetAssistantStatus;

internal sealed class GetAssistantStatusQueryHandler(IAssistantConfigurationStore store)
    : IQueryHandler<GetAssistantStatusQuery, AssistantStatusResponse>
{
    public async Task<Result<AssistantStatusResponse>> Handle(
        GetAssistantStatusQuery query,
        CancellationToken cancellationToken)
    {
        AssistantConfiguration configuration = await store.LoadAsync(cancellationToken);
        return new AssistantStatusResponse(
            configuration is { Provisioned: true, Enabled: true },
            AssistantProviders.IsReadyForChat(configuration));
    }
}
