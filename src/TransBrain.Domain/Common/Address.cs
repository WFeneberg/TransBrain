namespace TransBrain.Domain.Common;

public sealed record Address
{
    private const int CountryLength = 2;

    private Address(string name, string street, string postalCode, string city, string country)
    {
        Name = name;
        Street = street;
        PostalCode = postalCode;
        City = city;
        Country = country;
    }

    public string Name { get; }

    public string Street { get; }

    public string PostalCode { get; }

    public string City { get; }

    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    public string Country { get; }

    public static Result<Address> Create(
        string? name,
        string? street,
        string? postalCode,
        string? city,
        string? country)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("Address.NameRequired", "Name must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(street))
        {
            return Error.Validation("Address.StreetRequired", "Street must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(postalCode))
        {
            return Error.Validation("Address.PostalCodeRequired", "Postal code must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            return Error.Validation("Address.CityRequired", "City must not be empty.");
        }

        string trimmedCountry = country?.Trim() ?? string.Empty;

        // Two ASCII letters. Validate shape on the TRIMMED input, before any case conversion:
        // ToUpperInvariant() can change a string's length on some platforms (e.g. "ß" -> "SS"
        // under full Unicode case folding), which would let a single non-ASCII character slip
        // past a length check performed after uppercasing. A full ISO 3166 table would be a
        // data-maintenance burden this product does not need yet; the shape check catches the
        // mistakes that actually happen (a three-letter code, a country name, an empty field).
        if (trimmedCountry.Length != CountryLength || !trimmedCountry.All(char.IsAsciiLetter))
        {
            return Error.Validation(
                "Address.CountryInvalid",
                "Country must be an ISO 3166-1 alpha-2 code, for example 'DE'.");
        }

        string normalisedCountry = trimmedCountry.ToUpperInvariant();

        return new Address(
            name.Trim(),
            street.Trim(),
            postalCode.Trim(),
            city.Trim(),
            normalisedCountry);
    }
}
