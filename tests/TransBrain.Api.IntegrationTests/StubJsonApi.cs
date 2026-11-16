using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TransBrain.Api.IntegrationTests;

/// <summary>
/// A minimal in-process HTTP server for the REST-API import tests: answers every GET with one
/// canned JSON body and records the Authorization header it received. TcpListener instead of
/// HttpListener keeps it free of URL-ACL quirks on Windows runners.
/// </summary>
public sealed class StubJsonApi(string json, int statusCode = 200) : IDisposable
{
    private readonly TcpListener _listener = Started();
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _serving;

    private static TcpListener Started()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return listener;
    }

    public string BaseUrl
    {
        get
        {
            _serving ??= Task.Run(ServeAsync);
            return $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";
        }
    }

    public string? LastAuthorization { get; private set; }

    public string? LastBody { get; private set; }

    private async Task ServeAsync()
    {
        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                using TcpClient client = await _listener.AcceptTcpClientAsync(_shutdown.Token);
                await HandleAsync(client);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (SocketException)
        {
            // Listener stopped while accepting.
        }
    }

    private async Task HandleAsync(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
        int contentLength = 0;
        bool chunked = false;
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
        {
            if (line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
            {
                LastAuthorization = line["Authorization:".Length..].Trim();
            }

            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                _ = int.TryParse(line["Content-Length:".Length..].Trim(), out contentLength);
            }

            if (line.StartsWith("Transfer-Encoding:", StringComparison.OrdinalIgnoreCase)
                && line.Contains("chunked", StringComparison.OrdinalIgnoreCase))
            {
                chunked = true;
            }
        }

        // Drain a request body (POSTs) before answering: closing the socket while the client is
        // still sending would reset the connection instead of delivering the canned response.
        // The ASCII decoder maps one byte to one char, so byte counts work as char counts here.
        var bodyText = new StringBuilder();
        if (chunked)
        {
            while ((line = await reader.ReadLineAsync()) is not null)
            {
                if (!int.TryParse(line.Trim(), System.Globalization.NumberStyles.HexNumber, null, out int size))
                {
                    break; // malformed; give up draining and just answer
                }

                if (size == 0)
                {
                    await reader.ReadLineAsync(); // trailing CRLF after the last chunk
                    break;
                }

                bodyText.Append(await DrainAsync(reader, size));
                await reader.ReadLineAsync(); // CRLF terminating this chunk's data
            }
        }
        else if (contentLength > 0)
        {
            bodyText.Append(await DrainAsync(reader, contentLength));
        }

        LastBody = bodyText.Length > 0 ? bodyText.ToString() : null;

        byte[] body = Encoding.UTF8.GetBytes(json);
        string head =
            $"HTTP/1.1 {statusCode} {(HttpStatusCode)statusCode}\r\n" +
            "Content-Type: application/json\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "Connection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
        await stream.WriteAsync(body);
        await stream.FlushAsync();
    }

    private static async Task<string> DrainAsync(StreamReader reader, int count)
    {
        char[] buffer = new char[count];
        int read = 0;
        while (read < count)
        {
            int chunk = await reader.ReadAsync(buffer.AsMemory(read, count - read));
            if (chunk == 0)
            {
                break;
            }

            read += chunk;
        }

        return new string(buffer, 0, read);
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _listener.Stop();
        try
        {
            _serving?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Shutdown races are irrelevant to the test outcome.
        }

        _shutdown.Dispose();
    }
}
