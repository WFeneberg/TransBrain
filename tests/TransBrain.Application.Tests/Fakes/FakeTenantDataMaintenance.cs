using System.Text.Json;
using TransBrain.Application.Abstractions;

namespace TransBrain.Application.Tests.Fakes;

public sealed class StubTenantContext(Guid tenantId) : ITenantContext
{
    public Guid TenantId { get; } = tenantId;
}

public sealed class FakeTenantDataMaintenance : ITenantDataMaintenance
{
    public string SchemaVersion { get; set; } = "20270101000000_Current";

    public TenantBackupDocument? ExportResult { get; set; }

    public IReadOnlyDictionary<string, int> ImportResult { get; set; } =
        new Dictionary<string, int> { ["vehicles"] = 2, ["drivers"] = 1 };

    public TenantBackupDataException? ImportFailure { get; set; }

    public TenantBackupDocument? LastImportedBackup { get; private set; }

    public int ImportCallCount { get; private set; }

    public Task<TenantBackupDocument> ExportAsync(CancellationToken cancellationToken)
        => Task.FromResult(ExportResult ?? throw new InvalidOperationException("No ExportResult configured."));

    public Task<IReadOnlyDictionary<string, int>> ImportAsync(
        TenantBackupDocument backup, CancellationToken cancellationToken)
    {
        ImportCallCount++;
        LastImportedBackup = backup;
        return ImportFailure is null ? Task.FromResult(ImportResult) : Task.FromException<IReadOnlyDictionary<string, int>>(ImportFailure);
    }

    public Task<string> GetSchemaVersionAsync(CancellationToken cancellationToken)
        => Task.FromResult(SchemaVersion);

    public static TenantBackupDocument Document(
        Guid tenantId,
        string schemaVersion = "20270101000000_Current",
        int formatVersion = TenantBackupDocument.Version,
        string fileFormat = TenantBackupDocument.Format)
        => new(
            fileFormat,
            formatVersion,
            schemaVersion,
            tenantId,
            new DateTimeOffset(2027, 5, 1, 12, 0, 0, TimeSpan.Zero),
            new Dictionary<string, JsonElement>
            {
                ["vehicles"] = JsonDocument.Parse("""[{"Id":"b1a6d5f2-0000-7000-8000-000000000001"}]""").RootElement.Clone(),
            });
}
