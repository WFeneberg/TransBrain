using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Domain.Tests.Orders;

public class OrderTollChargeTests
{
    private static readonly DateTimeOffset Now = new(2027, 2, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid OrderId = Guid.CreateVersion7();

    private static Result<OrderTollCharge> Create(
        string category = "D-015",
        int emptyKm = 0,
        int loadedKm = 0,
        decimal factorPerKm = 0m)
        => OrderTollCharge.Create(OrderId, category, emptyKm, loadedKm, factorPerKm, "disponent", Now);

    [Fact]
    public void Create_ValidArguments_SetsIdentityAndAuditStamp()
    {
        Result<OrderTollCharge> result = Create("A-0300", 120, 480, 0.3m);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(new
        {
            OrderId,
            TollCategory = "A-0300",
            EmptyKm = 120,
            LoadedKm = 480,
            FactorPerKm = 0.3m,
            ModifiedBy = "disponent",
            ModifiedAt = Now,
        });
        result.Value.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_Amounts_AreKilometresTimesFactorRoundedToCents()
    {
        OrderTollCharge toll = Create("D-0124", emptyKm: 33, loadedKm: 67, factorPerKm: 0.124m).Value;

        toll.EmptyAmount.Should().Be(4.09m);   // 33 × 0.124 = 4.092
        toll.LoadedAmount.Should().Be(8.31m);  // 67 × 0.124 = 8.308
    }

    [Fact]
    public void Create_MidpointAmount_RoundsAwayFromZero()
    {
        OrderTollCharge toll = Create(emptyKm: 1, loadedKm: 0, factorPerKm: 0.125m).Value;

        toll.EmptyAmount.Should().Be(0.13m);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyCategory_ReturnsValidationError(string category)
    {
        Result<OrderTollCharge> result = Create(category);

        result.Error!.Code.Should().Be("OrderTollCharge.CategoryRequired");
    }

    [Fact]
    public void Create_CategoryLongerThanCatalogCode_ReturnsValidationError()
    {
        Result<OrderTollCharge> result = Create(new string('x', 21));

        result.Error!.Code.Should().Be("OrderTollCharge.CategoryTooLong");
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(100_001, 0)]
    [InlineData(0, 100_001)]
    public void Create_KilometresOutOfRange_ReturnsValidationError(int emptyKm, int loadedKm)
    {
        Result<OrderTollCharge> result = Create(emptyKm: emptyKm, loadedKm: loadedKm);

        result.Error!.Code.Should().Be("OrderTollCharge.KilometersOutOfRange");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1000.01)]
    public void Create_FactorOutOfRange_ReturnsValidationError(decimal factor)
    {
        Result<OrderTollCharge> result = Create(factorPerKm: factor);

        result.Error!.Code.Should().Be("OrderTollCharge.FactorOutOfRange");
    }

    [Fact]
    public void Create_FactorWithMoreThanFourDecimals_ReturnsValidationError()
    {
        Result<OrderTollCharge> result = Create(factorPerKm: 0.12345m);

        result.Error!.Code.Should().Be("OrderTollCharge.FactorTooPrecise");
    }

    [Fact]
    public void Update_ValidArguments_ReplacesValuesAndRestamps()
    {
        OrderTollCharge toll = Create().Value;
        DateTimeOffset later = Now.AddHours(3);

        Result<OrderTollCharge> result = toll.Update("CH-055", 10, 20, 0.55m, "buchhaltung", later);

        result.IsSuccess.Should().BeTrue();
        toll.Should().BeEquivalentTo(new
        {
            TollCategory = "CH-055",
            EmptyKm = 10,
            LoadedKm = 20,
            FactorPerKm = 0.55m,
            ModifiedBy = "buchhaltung",
            ModifiedAt = later,
        });
    }

    [Fact]
    public void Update_InvalidArguments_LeaveTheRecordUntouched()
    {
        OrderTollCharge toll = Create("A1", 5, 10, 0.3m).Value;

        toll.Update("A2", -1, 10, 0.3m, "someone", Now.AddHours(1));

        toll.Should().BeEquivalentTo(new { TollCategory = "A1", EmptyKm = 5, ModifiedAt = Now });
    }

    [Fact]
    public void Create_OverlongModifiedBy_IsTruncatedNotRejected()
    {
        OrderTollCharge toll = OrderTollCharge.Create(
            OrderId, "A1", 0, 0, 0m, new string('u', 150), Now).Value;

        toll.ModifiedBy!.Length.Should().Be(100);
    }
}
