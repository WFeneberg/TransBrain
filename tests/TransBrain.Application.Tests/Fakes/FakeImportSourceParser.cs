using TransBrain.Application.Abstractions;
using TransBrain.Application.Features.Imports;
using TransBrain.Domain.Common;
using TransBrain.Domain.Imports;

namespace TransBrain.Application.Tests.Fakes;

/// <summary>Returns a canned parse result and records what it was asked to parse.</summary>
public sealed class FakeImportSourceParser : IImportSourceParser
{
    public Result<ImportTableSet>? NextFileResult { get; set; }

    public Result<ImportTableSet>? NextDatabaseResult { get; set; }

    public ImportSourceType? LastSourceType { get; private set; }

    public string? LastFileName { get; private set; }

    public byte[]? LastContent { get; private set; }

    public DatabaseImportProvider? LastProvider { get; private set; }

    public string? LastConnectionString { get; private set; }

    public Task<Result<ImportTableSet>> ParseFileAsync(
        ImportSourceType sourceType, string fileName, byte[] content, CancellationToken cancellationToken)
    {
        LastSourceType = sourceType;
        LastFileName = fileName;
        LastContent = content;
        return Task.FromResult(NextFileResult ?? new ImportTableSet([]));
    }

    public Task<Result<ImportTableSet>> ReadDatabaseAsync(
        DatabaseImportProvider provider, string connectionString, CancellationToken cancellationToken)
    {
        LastProvider = provider;
        LastConnectionString = connectionString;
        return Task.FromResult(NextDatabaseResult ?? new ImportTableSet([]));
    }
}
