using AwesomeAssertions;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Features.Maintenance.ListTenants;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Maintenance;

public class ListTenantsQueryHandlerTests
{
    private sealed class FakeTenantDirectory(IReadOnlyList<TenantSummary> tenants) : ITenantDirectory
    {
        public Task<IReadOnlyList<TenantSummary>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult(tenants);
    }

    [Fact]
    public async Task Handle_Always_ReturnsTheDirectoryList()
    {
        Guid tenantId = Guid.CreateVersion7();
        TenantSummary summary = new(tenantId, Customers: 2, Drivers: 1, Vehicles: 3,
            Orders: 4, Tours: 1, Invoices: 0, TotalRows: 11);
        ListTenantsQueryHandler handler = new(new FakeTenantDirectory([summary]));

        Result<IReadOnlyList<TenantSummary>> result =
            await handler.Handle(new ListTenantsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Should().Be(summary);
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsEmptyList()
    {
        ListTenantsQueryHandler handler = new(new FakeTenantDirectory([]));

        Result<IReadOnlyList<TenantSummary>> result =
            await handler.Handle(new ListTenantsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
