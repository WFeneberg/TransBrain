using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using TransBrain.Application.Common.Behaviors;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Common.Behaviors;

public class LoggingBehaviorTests
{
    private sealed record SampleCommand(string Name) : ICommand<string>;

    public sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    [Fact]
    public async Task Handle_SuccessfulResult_LogsInformation()
    {
        CapturingLogger<LoggingBehavior<SampleCommand, string>> logger = new();
        LoggingBehavior<SampleCommand, string> behavior = new(logger);

        await behavior.Handle(
            new SampleCommand("ok"),
            () => Task.FromResult(Result<string>.Success("done")),
            CancellationToken.None);

        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Level.Should().Be(LogLevel.Information);
        logger.Entries[0].Message.Should().Contain(nameof(SampleCommand));
    }

    [Fact]
    public async Task Handle_FailedResult_LogsWarningWithErrorCodeAndType()
    {
        CapturingLogger<LoggingBehavior<SampleCommand, string>> logger = new();
        LoggingBehavior<SampleCommand, string> behavior = new(logger);
        Error error = Error.Validation("Name", "Name must not be empty");

        await behavior.Handle(
            new SampleCommand(string.Empty),
            () => Task.FromResult(Result<string>.Failure(error)),
            CancellationToken.None);

        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Level.Should().Be(LogLevel.Warning);
        logger.Entries[0].Message.Should().Contain("Name").And.Contain("Validation");
    }
}
