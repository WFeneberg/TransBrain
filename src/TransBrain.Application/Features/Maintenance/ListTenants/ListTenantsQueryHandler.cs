using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Maintenance.ListTenants;

internal sealed class ListTenantsQueryHandler(ITenantDirectory directory)
    : IQueryHandler<ListTenantsQuery, IReadOnlyList<TenantSummary>>
{
    public async Task<Result<IReadOnlyList<TenantSummary>>> Handle(
        ListTenantsQuery query, CancellationToken cancellationToken)
        => Result<IReadOnlyList<TenantSummary>>.Success(await directory.ListAsync(cancellationToken));
}
