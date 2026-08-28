using AwesomeAssertions;
using FluentValidation;
using TransBrain.Application.Common.Behaviors;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Common.Behaviors;

public class ValidationBehaviorTests
{
    private sealed record SampleCommand(string Name) : ICommand<string>;

    private sealed class SampleCommandValidator : AbstractValidator<SampleCommand>
    {
        public SampleCommandValidator() => RuleFor(c => c.Name).NotEmpty();
    }

    [Fact]
    public async Task Handle_ValidRequest_CallsNextAndReturnsItsResult()
    {
        ValidationBehavior<SampleCommand, string> behavior = new([new SampleCommandValidator()]);
        bool nextCalled = false;

        Result<string> result = await behavior.Handle(
            new SampleCommand("ok"),
            () =>
            {
                nextCalled = true;
                return Task.FromResult(Result<string>.Success("done"));
            },
            CancellationToken.None);

        nextCalled.Should().BeTrue();
        result.Value.Should().Be("done");
    }

    [Fact]
    public async Task Handle_InvalidRequest_ReturnsValidationErrorWithoutCallingNext()
    {
        ValidationBehavior<SampleCommand, string> behavior = new([new SampleCommandValidator()]);
        bool nextCalled = false;

        Result<string> result = await behavior.Handle(
            new SampleCommand(string.Empty),
            () =>
            {
                nextCalled = true;
                return Task.FromResult(Result<string>.Success("done"));
            },
            CancellationToken.None);

        nextCalled.Should().BeFalse();
        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Name");
    }

    [Fact]
    public async Task Handle_NoValidatorsRegistered_CallsNext()
    {
        ValidationBehavior<SampleCommand, string> behavior = new([]);

        Result<string> result = await behavior.Handle(
            new SampleCommand(string.Empty),
            () => Task.FromResult(Result<string>.Success("done")),
            CancellationToken.None);

        result.Value.Should().Be("done");
    }
}
