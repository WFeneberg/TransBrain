using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Imports.CreateImportSession;

/// <summary>
/// Fetches a REST API's JSON response once and stores its normalized tables as a new import
/// session. The request headers (API keys, Authorization) are used for this single call and
/// never persisted or echoed back; the stored source name drops the URL's query string for the
/// same reason.
/// </summary>
public sealed record CreateApiImportSessionCommand(string Url, IReadOnlyList<ImportApiHeader> Headers)
    : ICommand<ImportSessionResponse>;
