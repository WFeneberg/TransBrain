using TransBrain.Domain.Common;

namespace TransBrain.Domain.Imports;

/// <summary>
/// One attempt to bring foreign data into TransBrain: an analyzed source (file or database)
/// whose normalized tables wait for a field mapping and an execution. The session records what
/// was imported so the history stays auditable; the normalized table data itself is stored by
/// Infrastructure next to the session, not on the aggregate.
/// </summary>
public sealed class ImportSession
{
    public const int MaxSourceNameLength = 260;

    // EF Core materialization only. Every other construction goes through Create.
    private ImportSession()
    {
        SourceName = null!;
        CreatedBy = null!;
    }

    private ImportSession(
        Guid id,
        ImportSourceType sourceType,
        string sourceName,
        string createdBy,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        SourceType = sourceType;
        SourceName = sourceName;
        CreatedBy = createdBy;
        CreatedAtUtc = createdAtUtc;
        Status = ImportSessionStatus.Ready;
    }

    public Guid Id { get; private set; }

    public ImportSourceType SourceType { get; private set; }

    /// <summary>The file name, or a display name of the database source (never the connection string).</summary>
    public string SourceName { get; private set; }

    public ImportSessionStatus Status { get; private set; }

    public string CreatedBy { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    /// <summary>The target entity of the last execution, for example <c>Customers</c>.</summary>
    public string? TargetEntity { get; private set; }

    /// <summary>The source table of the last execution.</summary>
    public string? TableName { get; private set; }

    public int ImportedCount { get; private set; }

    public int FailedCount { get; private set; }

    public static Result<ImportSession> Create(
        ImportSourceType sourceType,
        string sourceName,
        string? createdBy,
        DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return Error.Validation("ImportSession.SourceNameRequired", "Source name must not be empty.");
        }

        string trimmed = sourceName.Trim();
        if (trimmed.Length > MaxSourceNameLength)
        {
            trimmed = trimmed[..MaxSourceNameLength];
        }

        return new ImportSession(
            Guid.CreateVersion7(),
            sourceType,
            trimmed,
            string.IsNullOrWhiteSpace(createdBy) ? "unknown" : createdBy.Trim(),
            createdAtUtc);
    }

    /// <summary>
    /// Records the outcome of an execution. Re-execution is allowed on purpose: an operator fixes
    /// the mapping and retries, and the per-row duplicate checks of the target entities keep
    /// already-imported rows from doubling.
    /// </summary>
    public Result<Unit> RecordExecution(
        string targetEntity,
        string tableName,
        int importedCount,
        int failedCount,
        DateTimeOffset completedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(targetEntity))
        {
            return Error.Validation("ImportSession.TargetEntityRequired", "Target entity must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(tableName))
        {
            return Error.Validation("ImportSession.TableNameRequired", "Table name must not be empty.");
        }

        if (importedCount < 0 || failedCount < 0)
        {
            return Error.Validation("ImportSession.CountsNegative", "Counts must not be negative.");
        }

        TargetEntity = targetEntity.Trim();
        TableName = tableName.Trim();
        ImportedCount = importedCount;
        FailedCount = failedCount;
        CompletedAtUtc = completedAtUtc;
        Status = failedCount == 0
            ? ImportSessionStatus.Completed
            : importedCount == 0
                ? ImportSessionStatus.Failed
                : ImportSessionStatus.CompletedWithErrors;

        return Unit.Value;
    }
}
