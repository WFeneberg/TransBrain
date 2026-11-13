using AwesomeAssertions;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Features.Maintenance.BackupTenantData;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Maintenance;

public class BackupTenantDataQueryHandlerTests
{
    [Fact]
    public async Task Handle_Always_ReturnsExportedDocument()
    {
        Guid tenantId = Guid.CreateVersion7();
        FakeTenantDataMaintenance maintenance = new()
        {
            ExportResult = FakeTenantDataMaintenance.Document(tenantId),
        };
        BackupTenantDataQueryHandler handler = new(maintenance);

        Result<TenantBackupDocument> result = await handler.Handle(new BackupTenantDataQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FileFormat.Should().Be(TenantBackupDocument.Format);
        result.Value.TenantId.Should().Be(tenantId);
        result.Value.Tables.Should().ContainKey("vehicles");
    }
}
