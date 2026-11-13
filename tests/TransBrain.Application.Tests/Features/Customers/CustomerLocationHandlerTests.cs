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
        customerId, name, "Südbahnstraße 5", "1100", "Wien", "AT",
        Contacts: [new CustomerLocationContactData("Hans Huber", Email: "h.huber@muster.at"), new CustomerLocationContactData("Eva Berger", "+43 1 222")]);

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
        result.Value.Contacts.Select(c => c.Name).Should().Equal("Hans Huber", "Eva Berger");
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
            CustomerLocation.Create(customer.Id, "Lager Süd", Address.Create("Lager Süd", "S 5", "1100", "Wien", "AT").Value, []).Value,
            CustomerLocation.Create(other.Id, "Fremd", Address.Create("Fremd", "F 1", "1100", "Wien", "AT").Value, []).Value);

        ListCustomerLocationsQueryHandler handler = new(repository);

        Result<CustomerLocationResponse[]> result =
            await handler.Handle(new ListCustomerLocationsQuery(customer.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Name.Should().Be("Lager Süd");
    }

    [Fact]
    public async Task Add_WithOwnCompanyNameAndVatId_PersistsBoth()
    {
        InMemoryCustomerRepository repository = new();
        Customer customer = SeedCustomer(repository);
        AddCustomerLocationCommandHandler handler = new(repository);

        Result<CustomerLocationResponse> result = await handler.Handle(
            AddCommand(customer.Id) with { CompanyName = " Lager Süd Logistik GmbH ", VatId = " ATU99999999 " },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CompanyName.Should().Be("Lager Süd Logistik GmbH");
        result.Value.VatId.Should().Be("ATU99999999");
    }

    [Fact]
    public async Task Update_ClearingCompanyNameAndVatId_RemovesBoth()
    {
        InMemoryCustomerRepository repository = new();
        Customer customer = SeedCustomer(repository);
        CustomerLocation location = CustomerLocation.Create(
            customer.Id, "Lager Süd", Address.Create("Lager Süd", "S 5", "1100", "Wien", "AT").Value, [],
            companyName: "Lager Süd Logistik GmbH", vatId: "ATU99999999").Value;
        repository.SeedLocations(location);
        UpdateCustomerLocationCommandHandler handler = new(repository);

        Result<CustomerLocationResponse> result = await handler.Handle(
            new UpdateCustomerLocationCommand(
                customer.Id, location.Id, "Lager Süd", "S 5", "1100", "Wien", "AT", CompanyName: null, VatId: " "),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CompanyName.Should().BeNull();
        result.Value.VatId.Should().BeNull();
    }

    [Fact]
    public async Task Update_UnknownLocation_ReturnsNotFound()
    {
        InMemoryCustomerRepository repository = new();
        Customer customer = SeedCustomer(repository);
        UpdateCustomerLocationCommandHandler handler = new(repository);

        Result<CustomerLocationResponse> result = await handler.Handle(
            new UpdateCustomerLocationCommand(
                customer.Id, Guid.CreateVersion7(), "X", "Y", "1010", "Wien", "AT"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("CustomerLocation.NotFound");
    }

    [Fact]
    public async Task Update_ExistingLocation_ReplacesContacts()
    {
        InMemoryCustomerRepository repository = new();
        Customer customer = SeedCustomer(repository);
        CustomerLocation location = CustomerLocation.Create(
            customer.Id, "Lager Süd", Address.Create("Lager Süd", "S 5", "1100", "Wien", "AT").Value,
            [CustomerLocationContact.Create("Alt", null, null).Value]).Value;
        repository.SeedLocations(location);
        UpdateCustomerLocationCommandHandler handler = new(repository);

        Result<CustomerLocationResponse> result = await handler.Handle(
            new UpdateCustomerLocationCommand(
                customer.Id, location.Id, "Lager Süd", "S 5", "1100", "Wien", "AT",
                Contacts: [new CustomerLocationContactData("Neu", "+43 1 333"), new CustomerLocationContactData("Zweit")]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Contacts.Should().Equal(new CustomerLocationContactData("Neu", "+43 1 333"), new CustomerLocationContactData("Zweit"));
    }

    [Fact]
    public async Task Delete_ExistingLocation_RemovesIt()
    {
        InMemoryCustomerRepository repository = new();
        Customer customer = SeedCustomer(repository);
        CustomerLocation location = CustomerLocation.Create(
            customer.Id, "Lager Süd", Address.Create("Lager Süd", "S 5", "1100", "Wien", "AT").Value,
            []).Value;
        repository.SeedLocations(location);
        DeleteCustomerLocationCommandHandler handler = new(repository);

        Result<Unit> result = await handler.Handle(
            new DeleteCustomerLocationCommand(customer.Id, location.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Locations.Should().BeEmpty();
    }
}
