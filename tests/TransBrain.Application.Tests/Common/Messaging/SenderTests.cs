using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Common.Messaging;

public class SenderTests
{
    internal sealed record EchoCommand(string Text) : ICommand<string>;

    internal sealed class EchoCommandHandler : ICommandHandler<EchoCommand, string>
    {
        public Task<Result<string>> Handle(EchoCommand command, CancellationToken cancellationToken)
            => Task.FromResult(Result<string>.Success(command.Text));
    }

    internal sealed record FailingQuery : IQuery<string>;

    internal sealed class FailingQueryHandler : IQueryHandler<FailingQuery, string>
    {
        public Task<Result<string>> Handle(FailingQuery query, CancellationToken cancellationToken)
            => Task.FromResult(Result<string>.Failure(Error.NotFound("Q.NotFound", "nothing here")));
    }

    internal sealed class SuffixBehavior : IPipelineBehavior<EchoCommand, string>
    {
        public async Task<Result<string>> Handle(
            EchoCommand request,
            RequestHandlerDelegate<string> next,
            CancellationToken cancellationToken)
        {
            Result<string> result = await next();
            return result.IsSuccess ? Result<string>.Success(result.Value + "!") : result;
        }
    }

    private static ISender BuildSender(Action<IServiceCollection>? configure = null)
    {
        ServiceCollection services = new();
        services.AddScoped<ISender, Sender>();
        services.AddScoped<ICommandHandler<EchoCommand, string>, EchoCommandHandler>();
        services.AddScoped<IQueryHandler<FailingQuery, string>, FailingQueryHandler>();
        configure?.Invoke(services);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Fact]
    public async Task Send_CommandWithRegisteredHandler_ReturnsHandlerResult()
    {
        ISender sender = BuildSender();

        Result<string> result = await sender.Send(new EchoCommand("hello"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("hello");
    }

    [Fact]
    public async Task Send_QueryWithFailingHandler_PropagatesError()
    {
        ISender sender = BuildSender();

        Result<string> result = await sender.Send(new FailingQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Send_CommandWithBehavior_RunsBehaviorAroundHandler()
    {
        ISender sender = BuildSender(services =>
            services.AddScoped<IPipelineBehavior<EchoCommand, string>, SuffixBehavior>());

        Result<string> result = await sender.Send(new EchoCommand("hello"), CancellationToken.None);

        result.Value.Should().Be("hello!");
    }

    [Fact]
    public async Task Send_CommandWithoutRegisteredHandler_ThrowsInvalidOperationException()
    {
        ServiceCollection services = new();
        services.AddScoped<ISender, Sender>();
        ISender sender = services.BuildServiceProvider().GetRequiredService<ISender>();

        await FluentActions
            .Awaiting(() => sender.Send(new EchoCommand("hello"), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }
}
