using TransBrain.Application.Abstractions;
using TransBrain.Application.Features.VehicleCatalog;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Drivers;

/// <summary>Wire shape of <see cref="DriverPhoneNumber"/> ("Telefone"), used in driver write bodies and responses.</summary>
public sealed record DriverPhoneNumberData(string Number, string? Type = null, string? Comment = null)
{
    public static DriverPhoneNumberData From(DriverPhoneNumber phone) =>
        new(phone.Number, phone.Type, phone.Comment);

    /// <summary>
    /// Turns the wire phone numbers into domain ones. A missing list means "none". Each type is resolved
    /// against the phone-type catalog; a type the driver already has stays accepted even when its entry
    /// was deactivated in the meantime. Other field problems are already caught by the command
    /// validators, so a failure from the domain itself is only its own backstop.
    /// </summary>
    public static async Task<Result<List<DriverPhoneNumber>>> ToDomainAsync(
        IReadOnlyList<DriverPhoneNumberData>? phoneNumbers,
        IVehicleCatalogRepository catalog,
        IReadOnlyCollection<string> currentTypeCodes,
        CancellationToken cancellationToken)
    {
        List<DriverPhoneNumber> result = [with(capacity: phoneNumbers?.Count ?? 0)];
        int index = 0;
        foreach (DriverPhoneNumberData data in phoneNumbers ?? [])
        {
            string? submitted = OptionalText.Normalize(data.Type);
            string? currentCode = currentTypeCodes.FirstOrDefault(
                code => string.Equals(code, submitted, StringComparison.OrdinalIgnoreCase));
            Result<string?> type = await VehicleCatalogLookup.ResolveAsync(
                catalog, VehicleCatalogKind.PhoneType, data.Type, currentCode,
                $"PhoneNumbers[{index}].Type", cancellationToken);
            if (!type.IsSuccess)
            {
                return type.Error!;
            }

            Result<DriverPhoneNumber> phone = DriverPhoneNumber.Create(data.Number, type.Value, data.Comment);
            if (!phone.IsSuccess)
            {
                return phone.Error!;
            }

            result.Add(phone.Value);
            index++;
        }

        return result;
    }
}
