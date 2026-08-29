using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Drivers.DeleteDriver;

public sealed record DeleteDriverCommand(Guid Id) : ICommand<Unit>;
