using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;

namespace TransBrain.Domain.Tests.Customers;

public class CustomerTests
{
    private static Address ValidAddress => Address.Create("Muster GmbH", "Hauptstraße 1", "4020", "Linz", "AT").Value;

    [Fact]
    public void Create_ValidArguments_ReturnsCustomerWithTrimmedFields()
    {
        Result<Customer> result = Customer.Create(
            "  Muster GmbH  ", ValidAddress, "  Anna Muster ", " anna@muster.at ", null, "  ATU12345678 ", 30);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.Name.Should().Be("Muster GmbH");
        result.Value.ContactPerson.Should().Be("Anna Muster");
        result.Value.Email.Should().Be("anna@muster.at");
        result.Value.Phone.Should().BeNull();
        result.Value.VatId.Should().Be("ATU12345678");
        result.Value.PaymentTermDays.Should().Be(30);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_ReturnsValidationError(string name)
    {
        Result<Customer> result = Customer.Create(name, ValidAddress, null, null, null, null, 30);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Customer.NameRequired");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void Create_PaymentTermOutOfRange_ReturnsValidationError(int days)
    {
        Result<Customer> result = Customer.Create("Muster GmbH", ValidAddress, null, null, null, null, days);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Customer.PaymentTermDaysOutOfRange");
    }

    [Fact]
    public void Update_ValidArguments_ReplacesFields()
    {
        Customer customer = Customer.Create("Alt", ValidAddress, null, null, null, null, 30).Value;
        Address newAddress = Address.Create("Neu AG", "Ringstraße 9", "1010", "Wien", "AT").Value;

        Result<Customer> result = customer.Update("Neu AG", newAddress, "Bernd", null, "+43 1 234", null, 14);

        result.IsSuccess.Should().BeTrue();
        customer.Name.Should().Be("Neu AG");
        customer.Address.City.Should().Be("Wien");
        customer.ContactPerson.Should().Be("Bernd");
        customer.Phone.Should().Be("+43 1 234");
        customer.PaymentTermDays.Should().Be(14);
    }

    [Fact]
    public void Update_InvalidPaymentTerm_LeavesCustomerUnchanged()
    {
        Customer customer = Customer.Create("Alt", ValidAddress, null, null, null, null, 30).Value;

        Result<Customer> result = customer.Update("Neu", ValidAddress, null, null, null, null, 400);

        result.IsSuccess.Should().BeFalse();
        customer.Name.Should().Be("Alt");
        customer.PaymentTermDays.Should().Be(30);
    }
}
