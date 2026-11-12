using System.Globalization;
using System.Net.Http.Json;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Infrastructure.Routing;

/// <remarks>
/// Geocoding through Nominatim — either the self-hosted container the AppHost starts (default,
/// no external service, only knows addresses inside the imported map extract) or the public
/// nominatim.openstreetmap.org, chosen by <see cref="NominatimOptions.Mode"/>; the registration
/// in DependencyInjection points the HttpClient accordingly. Successful lookups are cached by
/// <c>LocationResolver</c>, so each address is looked up once — which also keeps use of the
/// public instance within its fair-use policy.
/// </remarks>
internal sealed class NominatimGeocoder(HttpClient httpClient) : IGeocoder
{
    public async Task<Result<GeoPoint>> GeocodeAsync(Address address, CancellationToken cancellationToken)
    {
        string text = $"{address.Street}, {address.PostalCode} {address.City}, {address.Country}";

        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(
                $"search?format=jsonv2&limit=1&q={Uri.EscapeDataString(text)}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Error.Unavailable(
                    "Geocoding.ServiceError",
                    $"Nominatim answered HTTP {(int)response.StatusCode}.");
            }

            List<NominatimPlace>? places = await response.Content.ReadFromJsonAsync<List<NominatimPlace>>(cancellationToken);
            if (places is null)
            {
                return Error.Unavailable("Geocoding.EmptyResponse", "Nominatim answered without a body.");
            }

            if (places.Count == 0)
            {
                return Error.NotFound("Geocoding.NoMatch", $"No place found for '{text}'.");
            }

            // Nominatim sends coordinates as strings, not numbers.
            if (!double.TryParse(places[0].Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out double latitude)
                || !double.TryParse(places[0].Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out double longitude))
            {
                return Error.Unavailable("Geocoding.Malformed", "Nominatim answered with unreadable coordinates.");
            }

            return GeoPoint.Create(latitude, longitude);
        }
        catch (HttpRequestException)
        {
            return Error.Unavailable("Geocoding.Unreachable", "Nominatim could not be reached.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Error.Unavailable("Geocoding.Timeout", "Nominatim did not answer in time.");
        }
    }

    private sealed record NominatimPlace(string Lat, string Lon);
}

/// <summary>
/// Settings for the Nominatim adapter. <see cref="Mode"/> picks between the self-hosted
/// container (default; the AppHost injects <see cref="BaseUrl"/>) and the public instance at
/// <see cref="OnlineBaseUrl"/>, which needs no API key but has a fair-use policy: identify
/// yourself (<see cref="UserAgent"/>, sent automatically) and stay around one request per second.
/// </summary>
public sealed class NominatimOptions
{
    public const string SectionName = "Nominatim";

    public MapServiceMode Mode { get; set; } = MapServiceMode.SelfHosted;

    /// <summary>Address of the self-hosted container, used when <see cref="Mode"/> is SelfHosted.</summary>
    public string BaseUrl { get; set; } = "http://localhost:8088/";

    /// <summary>Address of the public instance, used when <see cref="Mode"/> is Online.</summary>
    public string OnlineBaseUrl { get; set; } = "https://nominatim.openstreetmap.org/";

    /// <summary>
    /// Identifies this installation to the public instance, whose usage policy requires it.
    /// Replace with your own contact when operating Online mode in earnest.
    /// </summary>
    public string UserAgent { get; set; } = "TransBrain (+https://github.com/WFeneberg/TransBrain)";

    public bool UsesOnlineService => Mode == MapServiceMode.Online;

    public string EffectiveBaseUrl => UsesOnlineService ? OnlineBaseUrl : BaseUrl;
}
