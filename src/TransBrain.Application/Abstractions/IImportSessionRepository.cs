using TransBrain.Domain.Imports;

namespace TransBrain.Application.Abstractions;

public interface IImportSessionRepository
{
    /// <summary>Persists the session together with its normalized table data.</summary>
    Task AddAsync(ImportSession session, Features.Imports.ImportTableSet tables, CancellationToken cancellationToken);

    Task<ImportSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The normalized table data of a session; null when the session does not exist.</summary>
    Task<Features.Imports.ImportTableSet?> GetTableSetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ImportSession>> ListAsync(int take, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task RemoveAsync(ImportSession session, CancellationToken cancellationToken);
}
