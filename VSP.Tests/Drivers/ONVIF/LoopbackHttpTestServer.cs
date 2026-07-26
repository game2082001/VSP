using System.Net;
using System.Net.Sockets;
using System.Text;

namespace VSP.Tests.Drivers.ONVIF;

/// <summary>
/// Minimal test-only loopback HTTP server built on <see cref="HttpListener"/> (which
/// correctly speaks HttpClient-compatible HTTP/1.1 framing, unlike a hand-rolled raw-socket
/// responder). Bound to a specific loopback port, which does not require the URL-ACL
/// reservation that wildcard (`+`/`*`) prefixes need on Windows. Accepts one request,
/// captures its raw body, and writes back a scripted status/body — or holds the request
/// open (no response) to simulate a timeout. Not a reusable mock-server framework.
/// </summary>
internal sealed class LoopbackHttpTestServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _stopSignal = new(initialState: false);
    private readonly ManualResetEventSlim _requestReceivedSignal = new(initialState: false);

    public LoopbackHttpTestServer(string? responseBody, int statusCode = 200, string contentType = "application/soap+xml")
    {
        Port = GetFreeTcpPort();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();

        _thread = new Thread(() => Serve(responseBody, statusCode, contentType)) { IsBackground = true };
        _thread.Start();
    }

    public int Port { get; }

    public string ReceivedRequest { get; private set; } = string.Empty;

    public bool WaitForRequest(TimeSpan timeout)
    {
        return _requestReceivedSignal.Wait(timeout);
    }

    private void Serve(string? responseBody, int statusCode, string contentType)
    {
        try
        {
            var context = _listener.GetContext();

            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
            {
                ReceivedRequest = reader.ReadToEnd();
            }

            _requestReceivedSignal.Set();

            if (responseBody is null)
            {
                _stopSignal.Wait();
                context.Response.Abort();
                return;
            }

            var bodyBytes = Encoding.UTF8.GetBytes(responseBody);
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = bodyBytes.Length;
            context.Response.OutputStream.Write(bodyBytes, 0, bodyBytes.Length);
            context.Response.OutputStream.Close();
        }
        catch (Exception)
        {
            // Listener stopped or client disconnected; nothing to do in a test helper.
        }
    }

    public void Dispose()
    {
        _stopSignal.Set();
        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch (Exception)
        {
            // Best-effort cleanup in a test helper.
        }
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
