using System.Globalization;
using System.Net.Http.Json;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Routing;

namespace TransBrain.Infrastructure.Routing;

/// <remarks>
/// Geocoding through Nominatim (OpenStreetMap). It is free and needs no API key. Its usage policy
/// asks for at most one request per second and an identifying User-Agent (set where the client is
/// registered). Successful lookups are cached by <c>LocationResolver</c>, so each address is
/// looked up once. Routing stays on OpenRouteService, which has no free equivalent for trucks.
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
