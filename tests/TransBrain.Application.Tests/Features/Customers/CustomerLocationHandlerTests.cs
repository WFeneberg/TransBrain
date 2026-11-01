using AwesomeAssertions;
using TransBrain.Application.Features.Customers.CustomerLocations;
using TransBrain.Application.Features.Customers.CustomerLocations.AddCustomerLocation;
using TransBrain.Application.Features.Customers.CustomerLocations.DeleteCustomerLocation;
using TransBrain.Application.Features.Customers.CustomerLocations.ListCustomerLocations;
using TransBrain.Application.Features.Customers.CustomerLocations.UpdateCustomerLocation;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;

namespace TransBrain.Application.Tests.Features.Customers;

public class CustomerLocationHandlerTests
{
    private static Customer SeedCustomer(InMemoryCustomerRepository repository)
    {
        Customer customer = Customer.Create(
            "Muster GmbH",
            Address.Create("Muster GmbH", "Hauptstraße 1", "4020", "Linz", "AT").Value,
            null, null, null, null, 30).Value;
        repository.Seed(customer);
        return customer;
    }

    private static AddCustomerLocationCommand AddCommand(Guid customerId, string name = "Lager Süd") => new(
        customerId, name, "Südbahnstraße 5", "1100", "Wien", "AT", "Hans Huber", "h.huber@muster.at", null);

    [Fact]
    public async Task Add_KnownCustomer_PersistsLocationForThatCustomer()
    {
        InMemoryCustomerRepository repository = new();
        Customer customer = SeedCustomer(repository);
        AddCustomerLocationCommandHandler handler = new(repository);

        Result<CustomerLocationResponse> result = await handler.Handle(AddCommand(customer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CustomerId.Should().Be(customer.Id);
        result.Value.City.Should().Be("Wien");
        result.Value.ContactPerson.Should().Be("Hans Huber");
        repository.Locations.Should().ContainSingle();
    }

    [Fact]
    public async Task Add_UnknownCustomer_ReturnsNotFound()
    {
        InMemoryCustomerRepository repository = new();
        AddCustomerLocationCommandHandler handler = new(repository);

        Result<CustomerLocationResponse> result = await handler.Handle(AddCommand(Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Customer.NotFound");
        repository.Locations.Should().BeEmpty();
    }

    [Fact]
    public async Task List_ReturnsOnlyLocationsOfRequestedCustomer()
    {
        InMemoryCustomerRepository repository = new();
        Customer customer = SeedCustomer(repository);
        Customer other = Customer.Create(
            "Andere AG", Address.Create("Andere AG", "Weg 2", "1010", "Wien", "AT").Value,
            null, null, null, null, 30).Value;
        repository.Seed(other);
        repository.SeedLocations(
            CustomerLocation.Create(customer.Id, "Lager Süd", Address.Create("Lager Süd", "S 5", "1100", "Wien", "AT").Value, null, null, null).Value,
            CustomerLocation.Create(other.Id, "Fremd", Address.Create("Fremd", "F 1", "1100", "Wien", "AT").Value, null, null, null).Value);

        ListCustomerLocationsQueryHandler handler = new(repository);

        Result<CustomerLocationResponse[]> result =
            await handler.Handle(new ListCustomerLocationsQuery(customer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Name.Should().Be("Lager Süd");
    }

    [Fact]
    public async Task Update_UnknownLocation_ReturnsNotFound()
    {
        InMemoryCustomerRepository repository = new();
        Customer customer = SeedCustomer(repository);
        UpdateCustomerLocationCommandHandler handler = new(repository);

        Result<CustomerLocationResponse> result = await handler.Handle(
            new UpdateCustomerLocationCommand(
                customer.Id, Guid.CreateVersion7(), "X", "Y", "1010", "Wien", "AT", null, null, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("CustomerLocation.NotFound");
    }

    [Fact]
    public async Task Update_ExistingLocation_ChangesContactPerson()
    {
        InMemoryCustomerRepository repository = new();
        Customer customer = SeedCustomer(repository);
        CustomerLocation location = CustomerLocation.Create(
            customer.Id, "Lager Süd", Address.Create("Lager Süd", "S 5", "1100", "Wien", "AT").Value,
            "Alt", null, null).Value;
        repository.SeedLocations(location);
        UpdateCustomerLocationCommandHandler handler = new(repository);

        Result<CustomerLocationResponse> result = await handler.Handle(
            new UpdateCustomerLocationCommand(
                customer.Id, location.Id, "Lager Süd", "S 5", "1100", "Wien", "AT", "Neu", null, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ContactPerson.Should().Be("Neu");
    }

    [Fact]
    public async Task Delete_ExistingLocation_RemovesIt()
    {
        InMemoryCustomerRepository repository = new();
        Customer customer = SeedCustomer(repository);
        CustomerLocation location = CustomerLocation.Create(
            customer.Id, "Lager Süd", Address.Create("Lager Süd", "S 5", "1100", "Wien", "AT").Value,
            null, null, null).Value;
        repository.SeedLocations(location);
        DeleteCustomerLocationCommandHandler handler = new(repository);

        Result<Unit> result = await handler.Handle(
            new DeleteCustomerLocationCommand(customer.Id, location.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Locations.Should().BeEmpty();
    }
}
