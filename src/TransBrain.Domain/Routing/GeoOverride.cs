using TransBrain.Domain.Common;

namespace TransBrain.Domain.Routing;

/// <summary>
/// A manually maintained position for one address. The self-hosted geocoder only knows addresses
/// inside its imported map extract, so an operator can supplement a missing position or correct a
/// wrong one here. An override always wins over the geocoder.
/// </summary>
public sealed class GeoOverride
{
    // EF Core materialization only. Every other construction goes through Create.
    private GeoOverride()
    {
        Street = null!;
        PostalCode = null!;
        City = null!;
        Country = null!;
        AddressKey = null!;
        Position = null!;
    }

    private GeoOverride(
        Guid id,
        string street,
        string postalCode,
        string city,
        string country,
        GeoPoint position,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Street = street;
        PostalCode = postalCode;
        City = city;
        Country = country;
        AddressKey = BuildKey(street, postalCode, city, country);
        Position = position;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; private set; }

    public string Street { get; private set; }

    public string PostalCode { get; private set; }

    public string City { get; private set; }

    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    public string Country { get; private set; }

    /// <summary>
    /// Case-insensitive identity of the address, in the same shape the geocode cache uses. One
    /// override exists per key.
    /// </summary>
    public string AddressKey { get; private set; }

    public GeoPoint Position { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<GeoOverride> Create(
        string? street,
        string? postalCode,
        string? city,
        string? country,
        double latitude,
        double longitude,
        DateTimeOffset now)
    {
        // An override has no name of its own, but Address.Create carries the full validation and
        // normalisation rules for the remaining fields. The placeholder never leaves this method.
        Result<Address> address = Address.Create("override", street, postalCode, city, country);
        if (!address.IsSuccess)
        {
            return address.Error!;
        }

        Result<GeoPoint> position = GeoPoint.Create(latitude, longitude);
        if (!position.IsSuccess)
        {
            return position.Error!;
        }

        return new GeoOverride(
            Guid.CreateVersion7(),
            address.Value.Street,
            address.Value.PostalCode,
            address.Value.City,
            address.Value.Country,
            position.Value,
            now);
    }

    public Result<Unit> Reposition(double latitude, double longitude, DateTimeOffset now)
    {
        Result<GeoPoint> position = GeoPoint.Create(latitude, longitude);
        if (!position.IsSuccess)
        {
            return position.Error!;
        }

        Position = position.Value;
        UpdatedAt = now;
        return Unit.Value;
    }

    /// <summary>
    /// The normalised lookup key for an address. Matches the shape the geocode cache key uses, so
    /// both lookups agree on what "the same address" means.
    /// </summary>
    public static string BuildKey(string street, string postalCode, string city, string country)
        => $"{country.Trim()}|{postalCode.Trim()}|{city.Trim()}|{street.Trim()}".ToLowerInvariant();

    public static string BuildKey(Address address)
        => BuildKey(address.Street, address.PostalCode, address.City, address.Country);
}
