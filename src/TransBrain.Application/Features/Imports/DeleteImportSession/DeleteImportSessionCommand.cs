using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Imports.DeleteImportSession;

public sealed record DeleteImportSessionCommand(Guid Id) : ICommand<Unit>;
