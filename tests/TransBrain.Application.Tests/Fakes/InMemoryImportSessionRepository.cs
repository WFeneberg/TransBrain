using TransBrain.Application.Abstractions;
using TransBrain.Application.Features.Imports;
using TransBrain.Domain.Imports;

namespace TransBrain.Application.Tests.Fakes;

public sealed class InMemoryImportSessionRepository : IImportSessionRepository
{
    private readonly List<ImportSession> _sessions = [];
    private readonly Dictionary<Guid, ImportTableSet> _tableSets = [];

    public IReadOnlyList<ImportSession> Sessions => _sessions;

    public int SaveChangesCalls { get; private set; }

    public void Seed(ImportSession session, ImportTableSet tables)
    {
        _sessions.Add(session);
        _tableSets[session.Id] = tables;
    }

    public Task AddAsync(ImportSession session, ImportTableSet tables, CancellationToken cancellationToken)
    {
        Seed(session, tables);
        return Task.CompletedTask;
    }

    public Task<ImportSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_sessions.SingleOrDefault(s => s.Id == id));

    public Task<ImportTableSet?> GetTableSetAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_tableSets.TryGetValue(id, out ImportTableSet? tables) ? tables : null);

    public Task<IReadOnlyList<ImportSession>> ListAsync(int take, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ImportSession>>(
            _sessions.OrderByDescending(s => s.CreatedAtUtc).Take(take).ToList());

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCalls++;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(ImportSession session, CancellationToken cancellationToken)
    {
        _sessions.Remove(session);
        _tableSets.Remove(session.Id);
        return Task.CompletedTask;
    }
}
