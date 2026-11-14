using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Maintenance.ListTenants;

public sealed record ListTenantsQuery : IQuery<IReadOnlyList<TenantSummary>>;
