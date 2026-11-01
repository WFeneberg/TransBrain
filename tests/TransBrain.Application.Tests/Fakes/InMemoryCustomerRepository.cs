using TransBrain.Application.Abstractions;
using TransBrain.Application.Features.Customers.CustomerLocations.ListAllCustomerLocations;
using TransBrain.Domain.Customers;

namespace TransBrain.Application.Tests.Fakes;

public sealed class InMemoryCustomerRepository : ICustomerRepository
{
    private readonly List<Customer> _customers = [];
    private readonly List<CustomerLocation> _locations = [];

    public IReadOnlyList<Customer> Customers => _customers;

    public IReadOnlyList<CustomerLocation> Locations => _locations;

    public void Seed(params Customer[] customers) => _customers.AddRange(customers);

    public void SeedLocations(params CustomerLocation[] locations) => _locations.AddRange(locations);

    public Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        _customers.Add(customer);
        return Task.CompletedTask;
    }

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_customers.SingleOrDefault(c => c.Id == id));

    // Ordinal ordering, matching the EF repository's column collation.
    public Task<IReadOnlyList<Customer>> ListAsync(int skip, int take, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Customer>>(
            _customers
                .OrderBy(c => c.Name, StringComparer.Ordinal)
                .Skip(skip)
                .Take(take)
                .ToList());

    public Task<int> CountAsync(CancellationToken cancellationToken)
        => Task.FromResult(_customers.Count);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RemoveAsync(Customer customer, CancellationToken cancellationToken)
    {
        _customers.Remove(customer);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CustomerLocation>> ListLocationsAsync(Guid customerId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<CustomerLocation>>(
            _locations
                .Where(l => l.CustomerId == customerId)
                .OrderBy(l => l.Name, StringComparer.Ordinal)
                .ToList());

    public Task<CustomerLocation?> GetLocationAsync(Guid customerId, Guid locationId, CancellationToken cancellationToken)
        => Task.FromResult(_locations.SingleOrDefault(l => l.CustomerId == customerId && l.Id == locationId));

    public Task<CustomerLocation?> GetLocationByIdAsync(Guid locationId, CancellationToken cancellationToken)
        => Task.FromResult(_locations.SingleOrDefault(l => l.Id == locationId));

    public Task<IReadOnlyList<CustomerLocationListItem>> ListAllLocationsAsync(int skip, int take, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<CustomerLocationListItem>>(
            (from l in _locations
             join c in _customers on l.CustomerId equals c.Id
             orderby c.Name, l.Name
             select ToItem(l, c))
            .Skip(skip)
            .Take(take)
            .ToList());

    public Task<int> CountAllLocationsAsync(CancellationToken cancellationToken)
        => Task.FromResult(_locations.Count);

    private static CustomerLocationListItem ToItem(CustomerLocation l, Customer c) => new(
        l.Id, c.Id, c.Name, l.Name, l.Address.Street, l.Address.PostalCode, l.Address.City,
        l.Address.Country, l.ContactPerson, l.Email, l.Phone);

    public Task AddLocationAsync(CustomerLocation location, CancellationToken cancellationToken)
    {
        _locations.Add(location);
        return Task.CompletedTask;
    }

    public Task RemoveLocationAsync(CustomerLocation location, CancellationToken cancellationToken)
    {
        _locations.Remove(location);
        return Task.CompletedTask;
    }
}
