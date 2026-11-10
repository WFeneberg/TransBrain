using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Domain.Tests.Vehicles;

public class VehicleDocumentTests
{
    private static readonly Guid VehicleId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ValidData_KeepsValuesAndStamps()
    {
        VehicleDocument document = VehicleDocument.Create(
            VehicleId, " ADR ", new DateOnly(2024, 1, 31), new DateOnly(2025, 1, 31), " Erstprüfung ", "admin", Now).Value;

        document.Id.Should().NotBeEmpty();
        document.VehicleId.Should().Be(VehicleId);
        document.DocumentType.Should().Be("ADR");
        document.LastCheckedOn.Should().Be(new DateOnly(2024, 1, 31));
        document.ValidUntil.Should().Be(new DateOnly(2025, 1, 31));
        document.Remark.Should().Be("Erstprüfung");
        document.ModifiedBy.Should().Be("admin");
        document.ModifiedAt.Should().Be(Now);
    }

    [Fact]
    public void Create_OnlyType_Succeeds()
    {
        VehicleDocument document = VehicleDocument.Create(VehicleId, "UVV", null, null, "  ", null, Now).Value;

        document.LastCheckedOn.Should().BeNull();
        document.ValidUntil.Should().BeNull();
        document.Remark.Should().BeNull();
    }

    [Fact]
    public void Create_SameDayCheckAndExpiry_Succeeds()
    {
        DateOnly day = new(2025, 6, 1);

        VehicleDocument.Create(VehicleId, "ADR", day, day, null, null, Now).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankType_ReturnsValidationError(string type)
    {
        VehicleDocument.Create(VehicleId, type, null, null, null, null, Now).Error!.Code
            .Should().Be("VehicleDocument.TypeRequired");
    }

    [Fact]
    public void Create_TooLongType_ReturnsValidationError()
    {
        VehicleDocument.Create(VehicleId, new string('T', 21), null, null, null, null, Now).Error!.Code
            .Should().Be("VehicleDocument.TypeTooLong");
    }

    [Fact]
    public void Create_ValidUntilBeforeLastChecked_ReturnsValidationError()
    {
        Result<VehicleDocument> result = VehicleDocument.Create(
            VehicleId, "ADR", new DateOnly(2025, 2, 1), new DateOnly(2025, 1, 31), null, null, Now);

        result.Error!.Code.Should().Be("VehicleDocument.ValidUntilBeforeLastChecked");
    }

    [Fact]
    public void Create_DateBefore1900_ReturnsValidationError()
    {
        VehicleDocument.Create(VehicleId, "ADR", new DateOnly(1899, 1, 1), null, null, null, Now).Error!.Code
            .Should().Be("VehicleDocument.DateTooEarly");
    }

    [Fact]
    public void Create_TooLongRemark_ReturnsValidationError()
    {
        VehicleDocument.Create(VehicleId, "ADR", null, null, new string('R', 501), null, Now).Error!.Code
            .Should().Be("VehicleDocument.RemarkTooLong");
    }

    [Fact]
    public void Update_ValidData_ReplacesValuesAndRestamps()
    {
        VehicleDocument document = VehicleDocument.Create(VehicleId, "ADR", null, new DateOnly(2025, 1, 31), "a", "alice", Now).Value;
        DateTimeOffset later = Now.AddDays(1);

        document.Update("UVV", new DateOnly(2026, 1, 1), new DateOnly(2030, 1, 31), null, "bob", later);

        document.DocumentType.Should().Be("UVV");
        document.ValidUntil.Should().Be(new DateOnly(2030, 1, 31));
        document.Remark.Should().BeNull();
        document.ModifiedBy.Should().Be("bob");
        document.ModifiedAt.Should().Be(later);
    }

    [Fact]
    public void Update_InvalidData_LeavesDocumentUnchanged()
    {
        VehicleDocument document = VehicleDocument.Create(VehicleId, "ADR", null, null, "a", "alice", Now).Value;

        Result<VehicleDocument> result = document.Update(" ", null, null, "b", "bob", Now.AddDays(1));

        result.IsSuccess.Should().BeFalse();
        document.DocumentType.Should().Be("ADR");
        document.Remark.Should().Be("a");
        document.ModifiedBy.Should().Be("alice");
    }

    [Fact]
    public void Update_VeryLongUserName_IsTruncated()
    {
        VehicleDocument document = VehicleDocument.Create(
            VehicleId, "ADR", null, null, null, new string('u', 150), Now).Value;

        document.ModifiedBy.Should().HaveLength(Vehicle.MaxModifiedByLength);
    }
}
