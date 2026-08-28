using AwesomeAssertions;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Vehicles;

// VehicleResponse.From serializes VehicleType/VehicleStatus via ToString() (and EF Core persists
// them the same way via HasConversion<string>()), so both frontends and the database column
// depend on these exact strings. Renaming an enum member is compiler-silent for that contract -
// every C# call site tracks the rename automatically - so these tests intentionally hardcode the
// expected literals rather than referencing the enum members themselves: a rename changes
// Enum.GetValues()'s output but not these string literals, so the mismatch is caught here.
public class VehicleResponseContractTests
{
    [Fact]
    public void VehicleType_Members_MatchThePinnedContractStrings()
    {
        Enum.GetValues<VehicleType>().Select(t => t.ToString()).Should().BeEquivalentTo(
            ["Tractor", "RigidTruck", "Van"]);
    }

    [Fact]
    public void VehicleStatus_Members_MatchThePinnedContractStrings()
    {
        Enum.GetValues<VehicleStatus>().Select(s => s.ToString()).Should().BeEquivalentTo(
            ["Available", "InWorkshop", "Decommissioned"]);
    }
}
