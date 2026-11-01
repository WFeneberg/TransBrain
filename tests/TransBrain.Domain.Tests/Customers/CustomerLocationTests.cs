using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;

namespace TransBrain.Domain.Tests.Customers;

public class CustomerLocationTests
{
    private static Address SampleAddress(string city = "Wien") =>
        Address.Create("Lager Süd", "Südbahnstraße 5", "1100", city, "AT").Value;

    [Fact]
    public void Create_ValidArguments_ReturnsLocationWithTrimmedFields()
    {
        Guid customerId = Guid.CreateVersion7();

        Result<CustomerLocation> result = CustomerLocation.Create(
            customerId, "  Lager Süd ", SampleAddress(), " Hans Huber ", " h.huber@muster.at ", null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.CustomerId.Should().Be(customerId);
        result.Value.Name.Should().Be("Lager Süd");
        result.Value.ContactPerson.Should().Be("Hans Huber");
        result.Value.Email.Should().Be("h.huber@muster.at");
        result.Value.Phone.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_ReturnsValidationError(string name)
    {
        Result<CustomerLocation> result = CustomerLocation.Create(Guid.CreateVersion7(), name, SampleAddress(), null, null, null);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("CustomerLocation.NameRequired");
    }

    [Fact]
    public void Update_ValidArguments_ReplacesContactAndAddress()
    {
        CustomerLocation location = CustomerLocation.Create(
            Guid.CreateVersion7(), "Lager Süd", SampleAddress(), "Alt", null, null).Value;

        Result<CustomerLocation> result = location.Update(
            "Werk Nord", SampleAddress("Graz"), "Neu", "neu@muster.at", "+43 316 1");

        result.IsSuccess.Should().BeTrue();
        location.Name.Should().Be("Werk Nord");
        location.Address.City.Should().Be("Graz");
        location.ContactPerson.Should().Be("Neu");
        location.Phone.Should().Be("+43 316 1");
    }
}
