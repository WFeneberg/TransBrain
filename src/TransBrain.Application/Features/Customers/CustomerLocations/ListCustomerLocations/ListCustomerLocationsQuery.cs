using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Customers.CustomerLocations.ListCustomerLocations;

public sealed record ListCustomerLocationsQuery(Guid CustomerId) : IQuery<CustomerLocationResponse[]>;
