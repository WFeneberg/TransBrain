using Microsoft.EntityFrameworkCore;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Features.Customers.CustomerLocations.ListAllCustomerLocations;
using TransBrain.Domain.Customers;

namespace TransBrain.Infrastructure.Persistence.Repositories;

internal sealed class CustomerRepository(TransBrainDbContext context) : ICustomerRepository
{
    public async Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        await context.Customers.AddAsync(customer, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => context.Customers.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Customer>> ListAsync(int skip, int take, CancellationToken cancellationToken)
        => await context.Customers
            .OrderBy(c => c.Name)
            .Skip(skip)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<int> CountAsync(CancellationToken cancellationToken)
        => context.Customers.CountAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
        => context.SaveChangesAsync(cancellationToken);

    public Task RemoveAsync(Customer customer, CancellationToken cancellationToken)
    {
        context.Customers.Remove(customer);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<CustomerLocation>> ListLocationsAsync(
        Guid customerId, CancellationToken cancellationToken)
        => await context.CustomerLocations
            .Where(l => l.CustomerId == customerId)
            .OrderBy(l => l.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<CustomerLocation?> GetLocationAsync(Guid customerId, Guid locationId, CancellationToken cancellationToken)
        => context.CustomerLocations.SingleOrDefaultAsync(
            l => l.CustomerId == customerId && l.Id == locationId, cancellationToken);

    public Task<CustomerLocation?> GetLocationByIdAsync(Guid locationId, CancellationToken cancellationToken)
        => context.CustomerLocations.SingleOrDefaultAsync(l => l.Id == locationId, cancellationToken);

    public async Task<IReadOnlyList<CustomerLocationListItem>> ListAllLocationsAsync(
        int skip, int take, CancellationToken cancellationToken)
        => await context.CustomerLocations
            .Join(context.Customers, l => l.CustomerId, c => c.Id, (l, c) => new { l, c })
            .OrderBy(x => x.c.Name)
            .ThenBy(x => x.l.Name)
            .Skip(skip)
            .Take(take)
            .Select(x => new CustomerLocationListItem(
                x.l.Id,
                x.c.Id,
                x.c.Name,
                x.l.Name,
                x.l.Address.Street,
                x.l.Address.PostalCode,
                x.l.Address.City,
                x.l.Address.Country,
                x.l.ContactPerson,
                x.l.Email,
                x.l.Phone))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<int> CountAllLocationsAsync(CancellationToken cancellationToken)
        => context.CustomerLocations.CountAsync(cancellationToken);

    public async Task AddLocationAsync(CustomerLocation location, CancellationToken cancellationToken)
    {
        await context.CustomerLocations.AddAsync(location, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveLocationAsync(CustomerLocation location, CancellationToken cancellationToken)
    {
        context.CustomerLocations.Remove(location);
        await context.SaveChangesAsync(cancellationToken);
    }
}
