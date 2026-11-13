using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Imports;

namespace TransBrain.Domain.Tests.Imports;

public class ImportSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);

    private static ImportSession CreateSession()
        => ImportSession.Create(ImportSourceType.Csv, "kunden.csv", "admin", Now).Value;

    [Fact]
    public void Create_WithValidData_StartsReady()
    {
        Result<ImportSession> result = ImportSession.Create(
            ImportSourceType.Excel, "flotte.xlsx", "admin", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.SourceType.Should().Be(ImportSourceType.Excel);
        result.Value.SourceName.Should().Be("flotte.xlsx");
        result.Value.Status.Should().Be(ImportSessionStatus.Ready);
        result.Value.CreatedBy.Should().Be("admin");
        result.Value.CreatedAtUtc.Should().Be(Now);
        result.Value.CompletedAtUtc.Should().BeNull();
        result.Value.ImportedCount.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankSourceName_ReturnsValidationError(string sourceName)
    {
        Result<ImportSession> result = ImportSession.Create(ImportSourceType.Csv, sourceName, "admin", Now);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ImportSession.SourceNameRequired");
    }

    [Fact]
    public void Create_WithOverlongSourceName_TruncatesInsteadOfFailing()
    {
        string longName = new string('a', 500);

        Result<ImportSession> result = ImportSession.Create(ImportSourceType.Csv, longName, "admin", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.SourceName.Should().HaveLength(ImportSession.MaxSourceNameLength);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_WithoutUserName_FallsBackToUnknown(string? createdBy)
    {
        Result<ImportSession> result = ImportSession.Create(ImportSourceType.Csv, "kunden.csv", createdBy, Now);

        result.Value.CreatedBy.Should().Be("unknown");
    }

    [Fact]
    public void RecordExecution_WithoutFailures_MarksCompleted()
    {
        ImportSession session = CreateSession();

        Result<Unit> result = session.RecordExecution("Customers", "Kunden", 10, 0, Now.AddMinutes(5));

        result.IsSuccess.Should().BeTrue();
        session.Status.Should().Be(ImportSessionStatus.Completed);
        session.TargetEntity.Should().Be("Customers");
        session.TableName.Should().Be("Kunden");
        session.ImportedCount.Should().Be(10);
        session.FailedCount.Should().Be(0);
        session.CompletedAtUtc.Should().Be(Now.AddMinutes(5));
    }

    [Fact]
    public void RecordExecution_WithSomeFailures_MarksCompletedWithErrors()
    {
        ImportSession session = CreateSession();

        session.RecordExecution("Customers", "Kunden", 7, 3, Now);

        session.Status.Should().Be(ImportSessionStatus.CompletedWithErrors);
    }

    [Fact]
    public void RecordExecution_WhenNothingWasImported_MarksFailed()
    {
        ImportSession session = CreateSession();

        session.RecordExecution("Customers", "Kunden", 0, 5, Now);

        session.Status.Should().Be(ImportSessionStatus.Failed);
    }

    [Fact]
    public void RecordExecution_Twice_AllowsRetryAfterFixedMapping()
    {
        ImportSession session = CreateSession();
        session.RecordExecution("Customers", "Kunden", 0, 5, Now);

        Result<Unit> retry = session.RecordExecution("Customers", "Kunden", 5, 0, Now.AddMinutes(10));

        retry.IsSuccess.Should().BeTrue();
        session.Status.Should().Be(ImportSessionStatus.Completed);
        session.ImportedCount.Should().Be(5);
    }

    [Theory]
    [InlineData("", "Kunden", "ImportSession.TargetEntityRequired")]
    [InlineData("Customers", " ", "ImportSession.TableNameRequired")]
    public void RecordExecution_WithBlankNames_ReturnsValidationError(
        string targetEntity, string tableName, string expectedCode)
    {
        ImportSession session = CreateSession();

        Result<Unit> result = session.RecordExecution(targetEntity, tableName, 1, 0, Now);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(expectedCode);
    }

    [Fact]
    public void RecordExecution_WithNegativeCounts_ReturnsValidationError()
    {
        ImportSession session = CreateSession();

        Result<Unit> result = session.RecordExecution("Customers", "Kunden", -1, 0, Now);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("ImportSession.CountsNegative");
    }
}
