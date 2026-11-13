using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Features.Imports;
using TransBrain.Domain.Imports;
using TransBrain.Infrastructure.Persistence.Imports;

namespace TransBrain.Infrastructure.Persistence.Repositories;

internal sealed class ImportSessionRepository(TransBrainDbContext context) : IImportSessionRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task AddAsync(ImportSession session, ImportTableSet tables, CancellationToken cancellationToken)
    {
        await context.ImportSessions.AddAsync(session, cancellationToken);
        await context.Set<ImportSessionData>().AddAsync(new ImportSessionData
        {
            ImportSessionId = session.Id,
            Payload = Compress(tables)
        }, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<ImportSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => context.ImportSessions.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<ImportTableSet?> GetTableSetAsync(Guid id, CancellationToken cancellationToken)
    {
        ImportSessionData? data = await context.Set<ImportSessionData>()
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.ImportSessionId == id, cancellationToken);

        return data is null ? null : Decompress(data.Payload);
    }

    public async Task<IReadOnlyList<ImportSession>> ListAsync(int take, CancellationToken cancellationToken)
        => await context.ImportSessions
            .OrderByDescending(s => s.CreatedAtUtc)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => context.SaveChangesAsync(cancellationToken);

    public Task RemoveAsync(ImportSession session, CancellationToken cancellationToken)
    {
        context.ImportSessions.Remove(session);
        return Task.CompletedTask;
    }

    private static byte[] Compress(ImportTableSet tables)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest))
        {
            JsonSerializer.Serialize(gzip, tables, SerializerOptions);
        }

        return output.ToArray();
    }

    private static ImportTableSet? Decompress(byte[] payload)
    {
        using var input = new MemoryStream(payload);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        return JsonSerializer.Deserialize<ImportTableSet>(gzip, SerializerOptions);
    }
}
