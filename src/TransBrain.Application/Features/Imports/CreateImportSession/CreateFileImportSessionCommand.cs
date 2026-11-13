using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Imports.CreateImportSession;

/// <summary>Analyzes an uploaded file and stores its normalized tables as a new import session.</summary>
public sealed record CreateFileImportSessionCommand(string FileName, byte[] Content)
    : ICommand<ImportSessionResponse>;
