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
        result.Error.Failures.Should().NotBeNull();
        result.Error.Failures!.Should().ContainKey("Name");
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

    public sealed record TwoFieldCommand(string Name, string City) : ICommand<string>;

    public sealed class TwoFieldCommandValidator : AbstractValidator<TwoFieldCommand>
    {
        public TwoFieldCommandValidator()
        {
            RuleFor(c => c.Name).NotEmpty();
            RuleFor(c => c.City).NotEmpty();
        }
    }

    [Fact]
    public async Task Handle_TwoInvalidFields_ReportsBothKeyedByFieldName()
    {
        ValidationBehavior<TwoFieldCommand, string> behavior = new([new TwoFieldCommandValidator()]);

        Result<string> result = await behavior.Handle(
            new TwoFieldCommand(string.Empty, string.Empty),
            () => Task.FromResult(Result<string>.Success("done")),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Failures.Should().NotBeNull();
        result.Error.Failures!.Keys.Should().BeEquivalentTo(["Name", "City"]);
    }

    [Fact]
    public async Task Handle_OneFieldWithTwoRuleFailures_GroupsBothMessagesUnderThatField()
    {
        ValidationBehavior<TwoFieldCommand, string> behavior = new([new TwoRuleValidator()]);

        Result<string> result = await behavior.Handle(
            new TwoFieldCommand("x", "ok"),
            () => Task.FromResult(Result<string>.Success("done")),
            CancellationToken.None);

        result.Error!.Failures!["Name"].Should().HaveCount(2);
    }

    public sealed class TwoRuleValidator : AbstractValidator<TwoFieldCommand>
    {
        public TwoRuleValidator()
        {
            RuleFor(c => c.Name).MinimumLength(3);
            RuleFor(c => c.Name).Matches("^[A-Z]");
        }
    }
}
