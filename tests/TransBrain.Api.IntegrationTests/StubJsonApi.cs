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
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
        {
            if (line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
            {
                LastAuthorization = line["Authorization:".Length..].Trim();
            }
        }

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
