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
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Customers;

public class CustomerCommandHandlerTests
{
    private static CreateCustomerCommand ValidCreate => new(
        "Muster GmbH", "Hauptstraße 1", "4020", "Linz", "AT", "Anna Muster", "anna@muster.at", null, "ATU12345678", 30);

    [Fact]
    public async Task Create_ValidCommand_PersistsCustomerAndReturnsResponse()
    {
        InMemoryCustomerRepository repository = new();
        CreateCustomerCommandHandler handler = new(
            repository, InMemoryVehicleCatalogRepository.WithStandardEntries(), new InMemoryCacheService());

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
        CreateCustomerCommandHandler handler = new(
            repository, InMemoryVehicleCatalogRepository.WithStandardEntries(), new InMemoryCacheService());

        Result<CustomerResponse> result = await handler.Handle(
            ValidCreate with { Country = "Austria" }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Address.CountryInvalid");
        repository.Customers.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_NegativePaymentTerm_ReturnsDomainError()
    {
        CreateCustomerCommandHandler handler = new(
            new InMemoryCustomerRepository(),
            InMemoryVehicleCatalogRepository.WithStandardEntries(),
            new InMemoryCacheService());

        Result<CustomerResponse> result = await handler.Handle(
            ValidCreate with { PaymentTermDays = -5 }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Customer.PaymentTermDaysOutOfRange");
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsNotFound()
    {
        UpdateCustomerCommandHandler handler = new(
            new InMemoryCustomerRepository(),
            InMemoryVehicleCatalogRepository.WithStandardEntries(),
            new InMemoryCacheService());

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
        UpdateCustomerCommandHandler handler = new(
            repository, InMemoryVehicleCatalogRepository.WithStandardEntries(), new InMemoryCacheService());

        Result<CustomerResponse> result = await handler.Handle(
            new UpdateCustomerCommand(
                existing.Id, "Neu GmbH", "Ringstraße 9", "1010", "Wien", "AT", null, null, null, null, 14),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Neu GmbH");
        result.Value.PaymentTermDays.Should().Be(14);
    }

    [Fact]
    public async Task Create_WithAdditionalData_StoresItAndReturnsCanonicalClassCode()
    {
        InMemoryCustomerRepository repository = new();
        CreateCustomerCommandHandler handler = new(
            repository, InMemoryVehicleCatalogRepository.WithStandardEntries(), new InMemoryCacheService());
        CustomerAdditionalData additional = new(
            SecondaryName: "Werk Vils",
            SecondaryStreet: "Industriestraße 5",
            SecondaryPostalCode: "6682",
            SecondaryCity: "Vils",
            SecondaryCountry: "at",
            FoundedOn: new DateOnly(1985, 4, 1),
            WorkerCount: 120,
            SalariedEmployeeCount: 35,
            RequiresPalletCheck: true,
            ExcludeTollFromInvoice: true,
            ReimburseToll: false,
            CustomerClass: "hutervilsall",
            Comment: "  Nur werktags anliefern.  ");

        Result<CustomerResponse> result = await handler.Handle(
            ValidCreate with { Additional = additional }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Additional.Should().Be(additional with
        {
            SecondaryCountry = "AT",
            CustomerClass = "HuterVilsAll",
            Comment = "Nur werktags anliefern."
        });
        repository.Customers.Single().Additional.WorkerCount.Should().Be(120);
    }

    [Fact]
    public async Task Create_WithoutAdditionalData_ReturnsEmptyAdditionalData()
    {
        CreateCustomerCommandHandler handler = new(
            new InMemoryCustomerRepository(),
            InMemoryVehicleCatalogRepository.WithStandardEntries(),
            new InMemoryCacheService());

        Result<CustomerResponse> result = await handler.Handle(ValidCreate, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Additional.Should().Be(new CustomerAdditionalData());
    }

    [Fact]
    public async Task Create_UnknownCustomerClass_ReturnsValidationFailureForField()
    {
        InMemoryCustomerRepository repository = new();
        CreateCustomerCommandHandler handler = new(
            repository, InMemoryVehicleCatalogRepository.WithStandardEntries(), new InMemoryCacheService());

        Result<CustomerResponse> result = await handler.Handle(
            ValidCreate with { Additional = new CustomerAdditionalData(CustomerClass: "Unknown") },
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Failures.Should().ContainKey("Additional.CustomerClass");
        repository.Customers.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_InactiveCustomerClass_ReturnsValidationFailure()
    {
        InMemoryVehicleCatalogRepository catalog = new();
        catalog.Seed(VehicleCatalogKind.CustomerClass, "Old", "Alt", isActive: false);
        CreateCustomerCommandHandler handler = new(
            new InMemoryCustomerRepository(), catalog, new InMemoryCacheService());

        Result<CustomerResponse> result = await handler.Handle(
            ValidCreate with { Additional = new CustomerAdditionalData(CustomerClass: "Old") },
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Failures.Should().ContainKey("Additional.CustomerClass");
    }

    [Fact]
    public async Task Create_InvalidSecondaryCountry_ReturnsDomainError()
    {
        CreateCustomerCommandHandler handler = new(
            new InMemoryCustomerRepository(),
            InMemoryVehicleCatalogRepository.WithStandardEntries(),
            new InMemoryCacheService());

        Result<CustomerResponse> result = await handler.Handle(
            ValidCreate with { Additional = new CustomerAdditionalData(SecondaryCountry: "Austria") },
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Customer.SecondaryCountryInvalid");
    }

    [Fact]
    public async Task Update_KeepsInactiveCustomerClassAlreadyAssigned()
    {
        InMemoryVehicleCatalogRepository catalog = new();
        VehicleCatalogEntry entry = catalog.Seed(VehicleCatalogKind.CustomerClass, "Old", "Alt");
        Customer existing = Customer.Create(
            "Alt GmbH",
            Address.Create("Alt GmbH", "Weg 1", "4020", "Linz", "AT").Value,
            null, null, null, null, 30,
            CustomerAdditionalInfo.Create(customerClass: "Old").Value).Value;
        entry.Update("Alt", isActive: false);
        InMemoryCustomerRepository repository = new();
        repository.Seed(existing);
        UpdateCustomerCommandHandler handler = new(repository, catalog, new InMemoryCacheService());

        Result<CustomerResponse> result = await handler.Handle(
            new UpdateCustomerCommand(
                existing.Id, "Alt GmbH", "Weg 1", "4020", "Linz", "AT", null, null, null, null, 30,
                new CustomerAdditionalData(WorkerCount: 3, CustomerClass: "Old")),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Additional.CustomerClass.Should().Be("Old");
        result.Value.Additional.WorkerCount.Should().Be(3);
    }

    [Fact]
    public async Task Update_WithoutAdditionalData_ClearsIt()
    {
        Customer existing = Customer.Create(
            "Alt GmbH",
            Address.Create("Alt GmbH", "Weg 1", "4020", "Linz", "AT").Value,
            null, null, null, null, 30,
            CustomerAdditionalInfo.Create(requiresPalletCheck: true, comment: "x").Value).Value;
        InMemoryCustomerRepository repository = new();
        repository.Seed(existing);
        UpdateCustomerCommandHandler handler = new(
            repository, InMemoryVehicleCatalogRepository.WithStandardEntries(), new InMemoryCacheService());

        Result<CustomerResponse> result = await handler.Handle(
            new UpdateCustomerCommand(
                existing.Id, "Alt GmbH", "Weg 1", "4020", "Linz", "AT", null, null, null, null, 30),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Additional.Should().Be(new CustomerAdditionalData());
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
