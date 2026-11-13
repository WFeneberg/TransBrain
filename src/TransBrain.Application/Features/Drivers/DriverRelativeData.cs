using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers;

/// <summary>Wire shape of <see cref="DriverRelative"/> ("Verwandte"), used in driver write bodies and responses.</summary>
public sealed record DriverRelativeData(
    string LastName,
    string? FirstName = null,
    string? Relationship = null,
    DateOnly? DateOfBirth = null,
    string? Phone = null)
{
    public static DriverRelativeData From(DriverRelative relative) => new(
        relative.LastName, relative.FirstName, relative.Relationship, relative.DateOfBirth, relative.Phone);

    /// <summary>
    /// Turns the wire relatives into domain relatives. A missing list means "none". Field problems
    /// are already caught by the command validators, so a failure here is only the domain's own
    /// backstop.
    /// </summary>
    public static Result<List<DriverRelative>> ToDomain(IReadOnlyList<DriverRelativeData>? relatives)
    {
        List<DriverRelative> result = [with(capacity: relatives?.Count ?? 0)];
        foreach (DriverRelativeData data in relatives ?? [])
        {
            Result<DriverRelative> relative = DriverRelative.Create(
                data.LastName, data.FirstName, data.Relationship, data.DateOfBirth, data.Phone);
            if (!relative.IsSuccess)
            {
                return relative.Error!;
            }

            result.Add(relative.Value);
        }

        return result;
    }
}
