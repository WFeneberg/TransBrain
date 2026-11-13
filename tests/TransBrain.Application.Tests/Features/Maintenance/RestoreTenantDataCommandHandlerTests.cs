using AwesomeAssertions;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Features.Customers;
using TransBrain.Application.Features.Drivers;
using TransBrain.Application.Features.Maintenance.RestoreTenantData;
using TransBrain.Application.Features.Routing;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Maintenance;

public class RestoreTenantDataCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    private static RestoreTenantDataCommandHandler CreateHandler(
        FakeTenantDataMaintenance maintenance, InMemoryCacheService cache)
        => new(maintenance, new StubTenantContext(TenantId), cache);

    [Fact]
    public async Task Handle_MatchingBackup_ImportsAndReturnsCounts()
    {
        FakeTenantDataMaintenance maintenance = new();
        InMemoryCacheService cache = new();
        RestoreTenantDataCommandHandler handler = CreateHandler(maintenance, cache);
        TenantBackupDocument backup = FakeTenantDataMaintenance.Document(TenantId, maintenance.SchemaVersion);

        Result<RestoreTenantDataResponse> result = await handler.Handle(
            new RestoreTenantDataCommand(backup), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalRows.Should().Be(3);
        result.Value.Tables.Should().Contain("vehicles", 2).And.Contain("drivers", 1);
        result.Value.SchemaVersion.Should().Be(maintenance.SchemaVersion);
        result.Value.BackupCreatedAt.Should().Be(backup.CreatedAt);
        maintenance.LastImportedBackup.Should().BeSameAs(backup);
    }

    [Fact]
    public async Task Handle_MatchingBackup_InvalidatesAllTenantCaches()
    {
        FakeTenantDataMaintenance maintenance = new();
        InMemoryCacheService cache = new();
        RestoreTenantDataCommandHandler handler = CreateHandler(maintenance, cache);
        TenantBackupDocument backup = FakeTenantDataMaintenance.Document(TenantId, maintenance.SchemaVersion);

        await handler.Handle(new RestoreTenantDataCommand(backup), CancellationToken.None);

        (await cache.GetGenerationAsync(VehicleCacheKeys.Prefix, CancellationToken.None)).Should().Be(1);
        (await cache.GetGenerationAsync(DriverCacheKeys.Prefix, CancellationToken.None)).Should().Be(1);
        (await cache.GetGenerationAsync(CustomerCacheKeys.Prefix, CancellationToken.None)).Should().Be(1);
        (await cache.GetGenerationAsync(GeocodeCache.Prefix, CancellationToken.None)).Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnsupportedFormatVersion_ReturnsValidationErrorWithoutImporting()
    {
        FakeTenantDataMaintenance maintenance = new();
        InMemoryCacheService cache = new();
        RestoreTenantDataCommandHandler handler = CreateHandler(maintenance, cache);
        TenantBackupDocument backup = FakeTenantDataMaintenance.Document(
            TenantId, maintenance.SchemaVersion, formatVersion: TenantBackupDocument.Version + 1);

        Result<RestoreTenantDataResponse> result = await handler.Handle(
            new RestoreTenantDataCommand(backup), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Backup.UnsupportedFormatVersion");
        maintenance.ImportCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForeignTenantBackup_ReturnsValidationErrorWithoutImporting()
    {
        FakeTenantDataMaintenance maintenance = new();
        InMemoryCacheService cache = new();
        RestoreTenantDataCommandHandler handler = CreateHandler(maintenance, cache);
        TenantBackupDocument backup = FakeTenantDataMaintenance.Document(
            Guid.CreateVersion7(), maintenance.SchemaVersion);

        Result<RestoreTenantDataResponse> result = await handler.Handle(
            new RestoreTenantDataCommand(backup), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Backup.TenantMismatch");
        maintenance.ImportCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SchemaMismatch_ReturnsConflictWithoutImporting()
    {
        FakeTenantDataMaintenance maintenance = new();
        InMemoryCacheService cache = new();
        RestoreTenantDataCommandHandler handler = CreateHandler(maintenance, cache);
        TenantBackupDocument backup = FakeTenantDataMaintenance.Document(TenantId, "20250101000000_Older");

        Result<RestoreTenantDataResponse> result = await handler.Handle(
            new RestoreTenantDataCommand(backup), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Backup.SchemaMismatch");
        maintenance.ImportCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ImportRejectsData_ReturnsValidationErrorAndKeepsCaches()
    {
        FakeTenantDataMaintenance maintenance = new()
        {
            ImportFailure = new TenantBackupDataException("Row 3 of table 'vehicles' is malformed."),
        };
        InMemoryCacheService cache = new();
        RestoreTenantDataCommandHandler handler = CreateHandler(maintenance, cache);
        TenantBackupDocument backup = FakeTenantDataMaintenance.Document(TenantId, maintenance.SchemaVersion);

        Result<RestoreTenantDataResponse> result = await handler.Handle(
            new RestoreTenantDataCommand(backup), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("Backup.InvalidData");
        result.Error.Message.Should().Contain("Row 3");
        cache.RemoveByPrefixCallCount.Should().Be(0);
    }
}
