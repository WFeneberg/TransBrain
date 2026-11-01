using AwesomeAssertions;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Customers.CustomerLocations.ListAllCustomerLocations;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;

namespace TransBrain.Application.Tests.Features.Customers;

public class ListAllCustomerLocationsQueryHandlerTests
{
    private static Address Sample(string name) => Address.Create(name, "Weg 1", "4020", "Linz", "AT").Value;

    [Fact]
    public async Task Handle_LocationsOfSeveralCustomers_ReturnsEachWithItsCustomerName()
    {
        InMemoryCustomerRepository repository = new();
        Customer alpha = Customer.Create("Alpha GmbH", Sample("Alpha GmbH"), null, null, null, null, 30).Value;
        Customer beta = Customer.Create("Beta AG", Sample("Beta AG"), null, null, null, null, 30).Value;
        repository.Seed(alpha, beta);
        repository.SeedLocations(
            CustomerLocation.Create(beta.Id, "Werk", Sample("Werk"), null, null, null).Value,
            CustomerLocation.Create(alpha.Id, "Lager", Sample("Lager"), null, null, null).Value);

        ListAllCustomerLocationsQueryHandler handler = new(repository);

        Result<PagedResult<CustomerLocationListItem>> result =
            await handler.Handle(new ListAllCustomerLocationsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(2);
        result.Value.Items.Select(i => (i.CustomerName, i.Name)).Should()
            .Equal(("Alpha GmbH", "Lager"), ("Beta AG", "Werk"));
    }
}
