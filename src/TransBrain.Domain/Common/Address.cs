namespace TransBrain.Domain.Common;

public sealed record Address
{
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

        string normalisedCountry = country?.Trim().ToUpperInvariant() ?? string.Empty;

        // Two ASCII letters. A full ISO 3166 table would be a data-maintenance burden this
        // product does not need yet; the shape check catches the mistakes that actually happen
        // (a three-letter code, a country name, an empty field).
        if (normalisedCountry.Length != 2 || !normalisedCountry.All(char.IsAsciiLetterUpper))
        {
            return Error.Validation(
                "Address.CountryInvalid",
                "Country must be an ISO 3166-1 alpha-2 code, for example 'DE'.");
        }

        return new Address(
            name.Trim(),
            street.Trim(),
            postalCode.Trim(),
            city.Trim(),
            normalisedCountry);
    }
}
