using System.Net.Http.Headers;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Features.Imports;
using TransBrain.Domain.Common;

namespace TransBrain.Infrastructure.Imports;

/// <summary>
/// Fetches one REST-API response and hands the JSON body to the JSON parser, so an API source
/// is analyzed exactly like an uploaded JSON file. Network failures, error statuses and
/// oversized bodies come back as <see cref="Error"/>s, never as exceptions; the URL and headers
/// are used for this single request and not kept.
/// </summary>
internal static class RestApiImportReader
{
    internal const string HttpClientName = "ImportRestApi";

    public static async Task<Result<ImportTableSet>> ReadAsync(
        HttpClient client,
        Uri url,
        IReadOnlyList<ImportApiHeader> headers,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        foreach (ImportApiHeader header in headers)
        {
            if (!request.Headers.TryAddWithoutValidation(header.Name, header.Value))
            {
                // Content headers (e.g. Content-Type) have no place on a GET without a body.
                return Error.Validation(
                    "Import.InvalidApiHeader", $"The header '{header.Name}' cannot be sent with the request.");
            }
        }

        try
        {
            using HttpResponseMessage response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return Error.Unavailable(
                    "Import.ApiRequestFailed",
                    $"The API answered with status {(int)response.StatusCode} ({response.StatusCode}).");
            }

            if (response.Content.Headers.ContentLength is > ImportLimits.MaxFileBytes)
            {
                return ResponseTooLarge();
            }

            byte[]? body = await ReadCappedAsync(response, cancellationToken);
            if (body is null)
            {
                return ResponseTooLarge();
            }

            if (body.Length == 0)
            {
                return Error.Validation("Import.EmptyApiResponse", "The API returned an empty response body.");
            }

            return JsonImportParser.Parse(TableNameSeed(url), body);
        }
        catch (HttpRequestException exception)
        {
            return Error.Unavailable("Import.ApiUnreachable", $"The API could not be reached: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient signals its own timeout as a cancellation; the caller's token is still live.
            return Error.Unavailable("Import.ApiUnreachable", "The API did not answer within the time limit.");
        }
        catch (IOException exception)
        {
            return Error.Unavailable(
                "Import.ApiUnreachable", $"The API response could not be read: {exception.Message}");
        }
    }

    private static Error ResponseTooLarge() => Error.Validation(
        "Import.ApiResponseTooLarge",
        $"The API response exceeds the limit of {ImportLimits.MaxFileBytes / (1024 * 1024)} MB.");

    /// <summary>Reads at most the file-size limit; one byte more and the response is rejected, not truncated.</summary>
    private static async Task<byte[]?> ReadCappedAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > ImportLimits.MaxFileBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    /// <summary>The JSON parser's fallback table name: the URL's last path segment, else its host.</summary>
    internal static string TableNameSeed(Uri url)
    {
        string? segment = url.Segments
            .Select(s => Uri.UnescapeDataString(s.Trim('/')))
            .LastOrDefault(s => s.Length > 0);
        return segment ?? url.Host;
    }
}
