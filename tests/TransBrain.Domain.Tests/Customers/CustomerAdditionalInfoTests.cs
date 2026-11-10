using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Domain.Tests.Customers;

public class CustomerAdditionalInfoTests
{
    private static Address ValidAddress => Address.Create("Muster GmbH", "Hauptstraße 1", "4020", "Linz", "AT").Value;

    [Fact]
    public void Create_AllValues_NormalizesTextAndUpperCasesCountry()
    {
        Result<CustomerAdditionalInfo> result = CustomerAdditionalInfo.Create(
            secondaryName: "  Werk Vils ",
            secondaryStreet: "Industriestraße 5",
            secondaryPostalCode: " 6682 ",
            secondaryCity: "Vils",
            secondaryCountry: " at ",
            foundedOn: new DateOnly(1985, 4, 1),
            workerCount: 120,
            salariedEmployeeCount: 35,
            requiresPalletCheck: true,
            excludeTollFromInvoice: true,
            reimburseToll: true,
            customerClass: " HuterVilsAll ",
            comment: "   ");

        result.IsSuccess.Should().BeTrue();
        result.Value.SecondaryName.Should().Be("Werk Vils");
        result.Value.SecondaryPostalCode.Should().Be("6682");
        result.Value.SecondaryCountry.Should().Be("AT");
        result.Value.FoundedOn.Should().Be(new DateOnly(1985, 4, 1));
        result.Value.WorkerCount.Should().Be(120);
        result.Value.SalariedEmployeeCount.Should().Be(35);
        result.Value.RequiresPalletCheck.Should().BeTrue();
        result.Value.ExcludeTollFromInvoice.Should().BeTrue();
        result.Value.ReimburseToll.Should().BeTrue();
        result.Value.CustomerClass.Should().Be("HuterVilsAll");
        result.Value.Comment.Should().BeNull();
    }

    [Fact]
    public void Create_NoValues_EqualsEmpty()
    {
        CustomerAdditionalInfo.Create().Value.Should().Be(CustomerAdditionalInfo.Empty);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("AUT")]
    [InlineData("A1")]
    public void Create_InvalidSecondaryCountry_ReturnsValidationError(string country)
    {
        Result<CustomerAdditionalInfo> result = CustomerAdditionalInfo.Create(secondaryCountry: country);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Customer.SecondaryCountryInvalid");
    }

    [Fact]
    public void Create_TooLongSecondaryPostalCode_ReturnsValidationError()
    {
        Result<CustomerAdditionalInfo> result = CustomerAdditionalInfo.Create(
            secondaryPostalCode: new string('1', CustomerAdditionalInfo.MaxPostalCodeLength + 1));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Customer.SecondaryAddressTooLong");
    }

    [Fact]
    public void Create_FoundedBeforeMinimumYear_ReturnsValidationError()
    {
        Result<CustomerAdditionalInfo> result = CustomerAdditionalInfo.Create(foundedOn: new DateOnly(1799, 12, 31));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Customer.FoundedOnTooEarly");
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(CustomerAdditionalInfo.MaxHeadcount + 1, 0)]
    public void Create_HeadcountOutOfRange_ReturnsValidationError(int workers, int salaried)
    {
        Result<CustomerAdditionalInfo> result = CustomerAdditionalInfo.Create(
            workerCount: workers, salariedEmployeeCount: salaried);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Customer.HeadcountOutOfRange");
    }

    [Fact]
    public void Create_TooLongCustomerClass_ReturnsValidationError()
    {
        Result<CustomerAdditionalInfo> result = CustomerAdditionalInfo.Create(customerClass: new string('A', VehicleCatalogEntry.MaxCodeLength + 1));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Customer.CustomerClassTooLong");
    }

    [Fact]
    public void Create_TooLongComment_ReturnsValidationError()
    {
        Result<CustomerAdditionalInfo> result = CustomerAdditionalInfo.Create(
            comment: new string('x', CustomerAdditionalInfo.MaxCommentLength + 1));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Customer.CommentTooLong");
    }

    [Fact]
    public void CustomerCreate_WithoutAdditional_UsesEmpty()
    {
        Customer customer = Customer.Create("Muster GmbH", ValidAddress, null, null, null, null, 30).Value;

        customer.Additional.Should().Be(CustomerAdditionalInfo.Empty);
    }

    [Fact]
    public void CustomerUpdate_WithAdditional_ReplacesIt()
    {
        Customer customer = Customer.Create(
            "Muster GmbH", ValidAddress, null, null, null, null, 30,
            CustomerAdditionalInfo.Create(requiresPalletCheck: true).Value).Value;
        CustomerAdditionalInfo replacement = CustomerAdditionalInfo.Create(workerCount: 7).Value;

        Result<Customer> result = customer.Update("Muster GmbH", ValidAddress, null, null, null, null, 30, replacement);

        result.IsSuccess.Should().BeTrue();
        customer.Additional.Should().Be(replacement);
        customer.Additional.RequiresPalletCheck.Should().BeFalse();
    }
}
