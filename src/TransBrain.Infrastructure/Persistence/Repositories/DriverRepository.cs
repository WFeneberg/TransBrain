using Microsoft.EntityFrameworkCore;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Infrastructure.Persistence.Repositories;

internal sealed class DriverRepository(TransBrainDbContext context) : IDriverRepository
{
    public async Task<Result<Driver>> AddAsync(Driver driver, CancellationToken cancellationToken)
    {
        await context.Drivers.AddAsync(driver, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return driver;
    }

    public Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => context.Drivers.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Driver>> ListAsync(
        int skip, int take, DriverStatus? status, CancellationToken cancellationToken)
        => await Filter(status)
            .OrderBy(d => d.LastName)
            .ThenBy(d => d.FirstName)
            .Skip(skip)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(DriverStatus? status, CancellationToken cancellationToken)
        => Filter(status).CountAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => context.SaveChangesAsync(cancellationToken);

    public Task RemoveAsync(Driver driver, CancellationToken cancellationToken)
    {
        context.Drivers.Remove(driver);
        return Task.CompletedTask;
    }

    private IQueryable<Driver> Filter(DriverStatus? status)
        => status is null ? context.Drivers : context.Drivers.Where(d => d.Status == status);
}
