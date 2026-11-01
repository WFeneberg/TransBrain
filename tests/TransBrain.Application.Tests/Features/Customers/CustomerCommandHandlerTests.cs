using AwesomeAssertions;
using TransBrain.Application.Features.Customers;
using TransBrain.Application.Features.Customers.CreateCustomer;
using TransBrain.Application.Features.Customers.DeleteCustomer;
using TransBrain.Application.Features.Customers.ListCustomers;
using TransBrain.Application.Features.Customers.UpdateCustomer;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;

namespace TransBrain.Application.Tests.Features.Customers;

public class CustomerCommandHandlerTests
{
    private static CreateCustomerCommand ValidCreate => new(
        "Muster GmbH", "Hauptstraße 1", "4020", "Linz", "AT", "Anna Muster", "anna@muster.at", null, "ATU12345678", 30);

    [Fact]
    public async Task Create_ValidCommand_PersistsCustomerAndReturnsResponse()
    {
        InMemoryCustomerRepository repository = new();
        CreateCustomerCommandHandler handler = new(repository, new InMemoryCacheService());

        Result<CustomerResponse> result = await handler.Handle(ValidCreate, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Muster GmbH");
        result.Value.City.Should().Be("Linz");
        result.Value.Country.Should().Be("AT");
        repository.Customers.Should().ContainSingle();
    }

    [Fact]
    public async Task Create_InvalidCountry_ReturnsAddressError()
    {
        InMemoryCustomerRepository repository = new();
        CreateCustomerCommandHandler handler = new(repository, new InMemoryCacheService());

        Result<CustomerResponse> result = await handler.Handle(
            ValidCreate with { Country = "Austria" }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Address.CountryInvalid");
        repository.Customers.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_NegativePaymentTerm_ReturnsDomainError()
    {
        CreateCustomerCommandHandler handler = new(new InMemoryCustomerRepository(), new InMemoryCacheService());

        Result<CustomerResponse> result = await handler.Handle(
            ValidCreate with { PaymentTermDays = -5 }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Customer.PaymentTermDaysOutOfRange");
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        UpdateCustomerCommandHandler handler = new(new InMemoryCustomerRepository(), new InMemoryCacheService());

        Result<CustomerResponse> result = await handler.Handle(
            new UpdateCustomerCommand(
                Guid.CreateVersion7(), "X", "Y", "1010", "Wien", "AT", null, null, null, null, 30),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Customer.NotFound");
    }

    [Fact]
    public async Task Update_ExistingCustomer_ChangesFieldsAndReturnsResponse()
    {
        Customer existing = Customer.Create(
            "Alt GmbH",
            Address.Create("Alt GmbH", "Weg 1", "4020", "Linz", "AT").Value,
            null, null, null, null, 30).Value;
        InMemoryCustomerRepository repository = new();
        repository.Seed(existing);
        UpdateCustomerCommandHandler handler = new(repository, new InMemoryCacheService());

        Result<CustomerResponse> result = await handler.Handle(
            new UpdateCustomerCommand(
                existing.Id, "Neu GmbH", "Ringstraße 9", "1010", "Wien", "AT", null, null, null, null, 14),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Neu GmbH");
        result.Value.PaymentTermDays.Should().Be(14);
    }

    [Fact]
    public async Task Delete_ExistingCustomer_RemovesIt()
    {
        Customer existing = Customer.Create(
            "Weg GmbH",
            Address.Create("Weg GmbH", "Weg 1", "4020", "Linz", "AT").Value,
            null, null, null, null, 30).Value;
        InMemoryCustomerRepository repository = new();
        repository.Seed(existing);
        DeleteCustomerCommandHandler handler = new(repository, new InMemoryCacheService());

        Result<Unit> result =
            await handler.Handle(new DeleteCustomerCommand(existing.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Customers.Should().BeEmpty();
    }

    [Fact]
    public async Task List_SecondPage_ReturnsRemainingCustomersWithTotal()
    {
        InMemoryCustomerRepository repository = new();
        foreach (string name in new[] { "Alpha", "Beta", "Gamma" })
        {
            repository.Seed(Customer.Create(
                name,
                Address.Create(name, "Weg 1", "4020", "Linz", "AT").Value,
                null, null, null, null, 30).Value);
        }

        ListCustomersQueryHandler handler = new(repository, new InMemoryCacheService());

        Result<PagedResult<CustomerResponse>> result =
            await handler.Handle(new ListCustomersQuery(Page: 2, PageSize: 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().ContainSingle().Which.Name.Should().Be("Gamma");
        result.Value.TotalCount.Should().Be(3);
    }
}
