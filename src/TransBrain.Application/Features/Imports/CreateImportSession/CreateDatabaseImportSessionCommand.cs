using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Imports.CreateImportSession;

/// <summary>
/// Reads a live SQL database and stores its normalized tables as a new import session. The
/// connection string is used once for the read and never persisted or echoed back.
/// </summary>
public sealed record CreateDatabaseImportSessionCommand(string Provider, string ConnectionString)
    : ICommand<ImportSessionResponse>;
