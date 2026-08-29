using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers;

internal static class LicenseClassParser
{
    public static Result<LicenseClass[]> Parse(IReadOnlyCollection<string> values)
    {
        List<LicenseClass> parsed = new(values.Count);

        foreach (string value in values)
        {
            // Enum.TryParse accepts numeric strings, so "99" would otherwise become an
            // undefined enum member and reach the database. IsDefined closes that gap.
            if (!Enum.TryParse(value, ignoreCase: true, out LicenseClass licenseClass)
                || !Enum.IsDefined(licenseClass))
            {
                return Error.Validation(
                    "Driver.UnknownLicenseClass",
                    $"'{value}' is not a known licence class.");
            }

            parsed.Add(licenseClass);
        }

        return parsed.ToArray();
    }
}
