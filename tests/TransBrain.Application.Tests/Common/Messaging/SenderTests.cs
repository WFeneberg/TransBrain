using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Common.Messaging;

public class SenderTests
{
    public sealed record EchoCommand(string Text) : ICommand<string>;

    public sealed class EchoCommandHandler : ICommandHandler<EchoCommand, string>
    {
        public Task<Result<string>> Handle(EchoCommand command, CancellationToken cancellationToken)
            => Task.FromResult(Result<string>.Success(command.Text));
    }

    public sealed record FailingQuery : IQuery<string>;

    public sealed class FailingQueryHandler : IQueryHandler<FailingQuery, string>
    {
        public Task<Result<string>> Handle(FailingQuery query, CancellationToken cancellationToken)
            => Task.FromResult(Result<string>.Failure(Error.NotFound("Q.NotFound", "nothing here")));
    }

    public sealed class SuffixBehavior : IPipelineBehavior<EchoCommand, string>
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

    public sealed class RecordingBehaviorA(List<string> log) : IPipelineBehavior<EchoCommand, string>
    {
        public async Task<Result<string>> Handle(
            EchoCommand request,
            RequestHandlerDelegate<string> next,
            CancellationToken cancellationToken)
        {
            log.Add("A-enter");
            Result<string> result = await next();
            log.Add("A-exit");
            return result;
        }
    }

    public sealed class RecordingBehaviorB(List<string> log) : IPipelineBehavior<EchoCommand, string>
    {
        public async Task<Result<string>> Handle(
            EchoCommand request,
            RequestHandlerDelegate<string> next,
            CancellationToken cancellationToken)
        {
            log.Add("B-enter");
            Result<string> result = await next();
            log.Add("B-exit");
            return result;
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
    public async Task Send_CommandWithTwoBehaviors_RunsFirstRegisteredOutermost()
    {
        List<string> log = [];
        ISender sender = BuildSender(services =>
        {
            services.AddSingleton(log);
            services.AddScoped<IPipelineBehavior<EchoCommand, string>, RecordingBehaviorA>();
            services.AddScoped<IPipelineBehavior<EchoCommand, string>, RecordingBehaviorB>();
        });

        await sender.Send(new EchoCommand("hello"), CancellationToken.None);

        log.Should().Equal("A-enter", "B-enter", "B-exit", "A-exit");
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
