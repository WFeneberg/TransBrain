using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Imports.GetImportSession;

public sealed record GetImportSessionQuery(Guid Id) : IQuery<ImportSessionResponse>;
