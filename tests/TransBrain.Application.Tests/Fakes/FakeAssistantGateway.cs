using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Fakes;

/// <summary>Echoes a fixed reply and records the turn it was asked to send.</summary>
public sealed class FakeAssistantGateway : IAssistantGateway
{
    public string Reply { get; set; } = "Gern geschehen!";

    /// <summary>Set to make every call answer this error, as an unreachable gateway would.</summary>
    public Error? Failure { get; set; }

    public AssistantChatTurn? LastTurn { get; private set; }

    public Task<Result<AssistantReply>> SendAsync(AssistantChatTurn turn, CancellationToken cancellationToken)
    {
        LastTurn = turn;
        return Task.FromResult(Failure is not null
            ? Result<AssistantReply>.Failure(Failure)
            : Result<AssistantReply>.Success(new AssistantReply(Reply)));
    }
}
