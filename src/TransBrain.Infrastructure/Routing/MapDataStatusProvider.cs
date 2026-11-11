using System.Text.Json;
using TransBrain.Application.Abstractions;

namespace TransBrain.Infrastructure.Routing;

/// <remarks>
/// Probes the same self-hosted containers the geocoder and route planner talk to, through the
/// HttpClient configurations registered for <see cref="NominatimGeocoder"/> and
/// <see cref="OpenRouteServiceClient"/>. Every probe is bounded by a short timeout and never
/// throws: "unavailable" is the answer the maintenance page exists to show, not an exception.
/// </remarks>
internal sealed class MapDataStatusProvider(IHttpClientFactory httpClientFactory) : IMapDataStatusProvider
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    public async Task<MapServiceStatus> GetGeocoderStatusAsync(CancellationToken cancellationToken)
    {
        HttpClient client = httpClientFactory.CreateClient(nameof(NominatimGeocoder));
        using JsonDocument? status = await ProbeAsync(client, "status?format=json", cancellationToken);

        // Nominatim answers {"status":0,"message":"OK","data_updated":"..."} when healthy;
        // a non-zero status means the database is broken even though HTTP said 200.
        if (status is null
            || !status.RootElement.TryGetProperty("status", out JsonElement code)
            || code.ValueKind is not JsonValueKind.Number
            || code.GetInt32() != 0)
        {
            return new MapServiceStatus(Available: false, OsmDataUpdatedAt: null);
        }

        DateTimeOffset? dataUpdated = null;
        if (status.RootElement.TryGetProperty("data_updated", out JsonElement updated)
            && updated.ValueKind is JsonValueKind.String
            && DateTimeOffset.TryParse(updated.GetString(), out DateTimeOffset parsed))
        {
            dataUpdated = parsed;
        }

        return new MapServiceStatus(Available: true, OsmDataUpdatedAt: dataUpdated);
    }

    public async Task<MapServiceStatus> GetRoutingStatusAsync(CancellationToken cancellationToken)
    {
        HttpClient client = httpClientFactory.CreateClient(nameof(OpenRouteServiceClient));
        using JsonDocument? health = await ProbeAsync(client, "v2/health", cancellationToken);

        bool ready = health is not null
            && health.RootElement.TryGetProperty("status", out JsonElement status)
            && status.ValueKind is JsonValueKind.String
            && string.Equals(status.GetString(), "ready", StringComparison.OrdinalIgnoreCase);
        if (!ready)
        {
            return new MapServiceStatus(Available: false, OsmDataUpdatedAt: null);
        }

        // The OSM data date sits in /v2/status; its exact shape varies between ORS versions,
        // so the first "osm_date" property anywhere in the document is taken and its absence
        // only costs the date, not the availability.
        using JsonDocument? detail = await ProbeAsync(client, "v2/status", cancellationToken);
        return new MapServiceStatus(
            Available: true,
            OsmDataUpdatedAt: detail is null ? null : FindOsmDate(detail.RootElement));
    }

    private static async Task<JsonDocument?> ProbeAsync(
        HttpClient client,
        string relativeUrl,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);

        try
        {
            using HttpResponseMessage response = await client.GetAsync(relativeUrl, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using Stream body = await response.Content.ReadAsStreamAsync(timeout.Token);
            return await JsonDocument.ParseAsync(body, cancellationToken: timeout.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
    }

    private static DateTimeOffset? FindOsmDate(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.NameEquals("osm_date")
                        && property.Value.ValueKind is JsonValueKind.String
                        && DateTimeOffset.TryParse(property.Value.GetString(), out DateTimeOffset parsed))
                    {
                        return parsed;
                    }

                    if (FindOsmDate(property.Value) is { } nested)
                    {
                        return nested;
                    }
                }

                return null;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    if (FindOsmDate(item) is { } nested)
                    {
                        return nested;
                    }
                }

                return null;
            default:
                return null;
        }
    }
}
