using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Domain.Tests.Orders;

public class OrderAdditionalChargeTests
{
    private static readonly DateTimeOffset Now = new(2027, 2, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid OrderId = Guid.CreateVersion7();

    private static Result<OrderAdditionalCharge> Create(
        string description = "Standzeit",
        decimal quantity = 1m,
        string? unit = "Std",
        decimal unitPrice = 0m)
        => OrderAdditionalCharge.Create(OrderId, description, quantity, unit, unitPrice, "disponent", Now);

    [Fact]
    public void Create_ValidArguments_SetsIdentityAndAuditStamp()
    {
        Result<OrderAdditionalCharge> result = Create("Wartezeit Rampe", 2.5m, "Std", 45m);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new
        {
            OrderId,
            Description = "Wartezeit Rampe",
            Quantity = 2.5m,
            Unit = "Std",
            UnitPrice = 45m,
            ModifiedBy = "disponent",
            ModifiedAt = Now,
        });
        result.Value.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_Total_IsQuantityTimesUnitPriceRoundedToCents()
    {
        OrderAdditionalCharge charge = Create(quantity: 3.33m, unitPrice: 9.99m).Value;

        charge.Total.Should().Be(33.27m); // 3.33 × 9.99 = 33.2667
    }

    [Fact]
    public void Create_WithoutUnit_IsAllowed()
    {
        OrderAdditionalCharge charge = Create(unit: "  ").Value;

        charge.Unit.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyDescription_ReturnsValidationError(string description)
    {
        Result<OrderAdditionalCharge> result = Create(description);

        result.Error!.Code.Should().Be("OrderAdditionalCharge.DescriptionRequired");
    }

    [Fact]
    public void Create_OverlongDescription_ReturnsValidationError()
    {
        Result<OrderAdditionalCharge> result = Create(new string('x', 201));

        result.Error!.Code.Should().Be("OrderAdditionalCharge.DescriptionTooLong");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1_000_001)]
    public void Create_QuantityOutOfRange_ReturnsValidationError(decimal quantity)
    {
        Result<OrderAdditionalCharge> result = Create(quantity: quantity);

        result.Error!.Code.Should().Be("OrderAdditionalCharge.QuantityOutOfRange");
    }

    [Fact]
    public void Create_QuantityWithMoreThanTwoDecimals_ReturnsValidationError()
    {
        Result<OrderAdditionalCharge> result = Create(quantity: 1.005m);

        result.Error!.Code.Should().Be("OrderAdditionalCharge.QuantityTooPrecise");
    }

    [Fact]
    public void Create_OverlongUnit_ReturnsValidationError()
    {
        Result<OrderAdditionalCharge> result = Create(unit: new string('u', 21));

        result.Error!.Code.Should().Be("OrderAdditionalCharge.UnitTooLong");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1_000_000.01)]
    public void Create_UnitPriceOutOfRange_ReturnsValidationError(decimal unitPrice)
    {
        Result<OrderAdditionalCharge> result = Create(unitPrice: unitPrice);

        result.Error!.Code.Should().Be("OrderAdditionalCharge.UnitPriceOutOfRange");
    }

    [Fact]
    public void Create_UnitPriceWithMoreThanTwoDecimals_ReturnsValidationError()
    {
        Result<OrderAdditionalCharge> result = Create(unitPrice: 1.005m);

        result.Error!.Code.Should().Be("OrderAdditionalCharge.UnitPriceTooPrecise");
    }

    [Fact]
    public void Update_ValidArguments_ReplacesValuesAndRestamps()
    {
        OrderAdditionalCharge charge = Create().Value;
        DateTimeOffset later = Now.AddHours(2);

        Result<OrderAdditionalCharge> result = charge.Update("Palettentausch", 33m, "Stk", 8.5m, "lager", later);

        result.IsSuccess.Should().BeTrue();
        charge.Should().BeEquivalentTo(new
        {
            Description = "Palettentausch",
            Quantity = 33m,
            Unit = "Stk",
            UnitPrice = 8.5m,
            Total = 280.5m,
            ModifiedBy = "lager",
            ModifiedAt = later,
        });
    }

    [Fact]
    public void Update_InvalidArguments_LeaveTheChargeUntouched()
    {
        OrderAdditionalCharge charge = Create("Standzeit", 1m, "Std", 40m).Value;

        charge.Update("", 1m, "Std", 40m, "someone", Now.AddHours(1));

        charge.Should().BeEquivalentTo(new { Description = "Standzeit", ModifiedAt = Now });
    }
}
