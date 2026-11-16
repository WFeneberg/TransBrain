using TransBrain.Api.Authorization;
using TransBrain.Api.Common;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Assistant;
using TransBrain.Application.Features.Assistant.ChatWithAssistant;
using TransBrain.Application.Features.Assistant.GetAssistantSettings;
using TransBrain.Application.Features.Assistant.GetAssistantStatus;
using TransBrain.Application.Features.Assistant.UpdateAssistantSettings;
using TransBrain.Domain.Common;

namespace TransBrain.Api.Endpoints;

/// <remarks>
/// Two very different audiences share this feature. Chat and status serve every signed-in role —
/// the assistant is meant to help everybody's daily work. The settings live in the system area:
/// provider, model and API key configure the installation's shared agent container, which is
/// infrastructure, not tenant data, and so belongs to the system administrator ("zentrale
/// Verwaltung"). The API key itself never leaves the server; responses only say whether one is
/// stored.
/// </remarks>
public sealed class AssistantEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/assistant").WithTags("Assistant");

        group.MapGet("/status", async (ISender sender, CancellationToken ct) =>
            {
                Result<AssistantStatusResponse> result = await sender.Send(new GetAssistantStatusQuery(), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("GetAssistantStatus")
            .Produces<AssistantStatusResponse>();

        group.MapPost("/chat", async (AssistantChatPayload payload, ISender sender, CancellationToken ct) =>
            {
                Result<AssistantChatResponse> result = await sender.Send(new ChatWithAssistantCommand(payload), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("ChatWithAssistant")
            .Produces<AssistantChatResponse>()
            .ProducesValidationProblem();

        RouteGroupBuilder system = app.MapSystemGroup().MapGroup("/assistant-settings").WithTags("System");

        system.MapGet("/", async (ISender sender, CancellationToken ct) =>
            {
                Result<AssistantSettingsResponse> result = await sender.Send(new GetAssistantSettingsQuery(), ct);
                return result.ToHttpResult();
            })
            .WithName("GetAssistantSettings")
            .Produces<AssistantSettingsResponse>();

        system.MapPut("/", async (AssistantSettingsPayload payload, ISender sender, CancellationToken ct) =>
            {
                Result<AssistantSettingsResponse> result =
                    await sender.Send(new UpdateAssistantSettingsCommand(payload), ct);
                return result.ToHttpResult();
            })
            .WithName("UpdateAssistantSettings")
            .Produces<AssistantSettingsResponse>()
            .ProducesValidationProblem();
    }
}
