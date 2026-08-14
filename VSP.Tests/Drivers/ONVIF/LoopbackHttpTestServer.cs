using System.Net;
using System.Net.Sockets;
using System.Text;

namespace VSP.Tests.Drivers.ONVIF;

/// <summary>
/// Minimal test-only loopback HTTP server built on <see cref="HttpListener"/> (which
/// correctly speaks HttpClient-compatible HTTP/1.1 framing, unlike a hand-rolled raw-socket
/// responder). Bound to a specific loopback port, which does not require the URL-ACL
/// reservation that wildcard (`+`/`*`) prefixes need on Windows. Accepts one request by
/// default, captures its raw body, and writes back a scripted status/body — or holds the
/// request open (no response) to simulate a timeout. An optional second scripted response
/// (mirroring <c>LoopbackRtspTestServer</c>'s firstResponse/secondResponse shape) lets a
/// single instance stand in for one ONVIF endpoint answering two sequential SOAP calls (e.g.
/// GetProfiles then GetStreamUri, both against the same Media service address). Not a
/// reusable mock-server framework.
/// </summary>
internal sealed class LoopbackHttpTestServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _stopSignal = new(initialState: false);
    private readonly ManualResetEventSlim _requestReceivedSignal = new(initialState: false);
    private readonly List<string> _receivedRequests = [];
    private readonly object _receivedRequestsGate = new();

    public LoopbackHttpTestServer(
        string? responseBody,
        int statusCode = 200,
        string contentType = "application/soap+xml",
        string? secondResponseBody = null,
        int secondStatusCode = 200)
    {
        Port = GetFreeTcpPort();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();

        var scriptedResponses = secondResponseBody is null
            ? [(responseBody, statusCode)]
            : new (string? Body, int StatusCode)[] { (responseBody, statusCode), (secondResponseBody, secondStatusCode) };

        _thread = new Thread(() => Serve(scriptedResponses, contentType)) { IsBackground = true };
        _thread.Start();
    }

    public int Port { get; }

    /// <summary>The most recently received request body (the only one, for single-response use).</summary>
    public string ReceivedRequest
    {
        get
        {
            lock (_receivedRequestsGate)
            {
                return _receivedRequests.Count > 0 ? _receivedRequests[^1] : string.Empty;
            }
        }
    }

    public IReadOnlyList<string> ReceivedRequests
    {
        get
        {
            lock (_receivedRequestsGate)
            {
                return _receivedRequests.ToList();
            }
        }
    }

    public bool WaitForRequest(TimeSpan timeout)
    {
        return _requestReceivedSignal.Wait(timeout);
    }

    private void Serve((string? Body, int StatusCode)[] scriptedResponses, string contentType)
    {
        try
        {
            foreach (var (body, statusCode) in scriptedResponses)
            {
                var context = _listener.GetContext();

                using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                {
                    var requestBody = reader.ReadToEnd();
                    lock (_receivedRequestsGate)
                    {
                        _receivedRequests.Add(requestBody);
                    }
                }

                _requestReceivedSignal.Set();

                if (body is null)
                {
                    _stopSignal.Wait();
                    context.Response.Abort();
                    return;
                }

                var bodyBytes = Encoding.UTF8.GetBytes(body);
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = contentType;
                context.Response.ContentLength64 = bodyBytes.Length;
                context.Response.OutputStream.Write(bodyBytes, 0, bodyBytes.Length);
                context.Response.OutputStream.Close();
            }
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
