using TransBrain.Domain.Customers;

namespace TransBrain.Application.Abstractions;

public interface ICustomerRepository
{
    Task AddAsync(Customer customer, CancellationToken cancellationToken);

    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Customer>> ListAsync(int skip, int take, CancellationToken cancellationToken);

    Task<int> CountAsync(CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task RemoveAsync(Customer customer, CancellationToken cancellationToken);

    Task<IReadOnlyList<CustomerLocation>> ListLocationsAsync(Guid customerId, CancellationToken cancellationToken);

    Task<CustomerLocation?> GetLocationAsync(Guid customerId, Guid locationId, CancellationToken cancellationToken);

    Task<CustomerLocation?> GetLocationByIdAsync(Guid locationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Features.Customers.CustomerLocations.ListAllCustomerLocations.CustomerLocationListItem>> ListAllLocationsAsync(
        int skip, int take, CancellationToken cancellationToken);

    Task<int> CountAllLocationsAsync(CancellationToken cancellationToken);

    Task AddLocationAsync(CustomerLocation location, CancellationToken cancellationToken);

    Task RemoveLocationAsync(CustomerLocation location, CancellationToken cancellationToken);
}
