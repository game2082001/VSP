using System.Net;
using System.Net.Sockets;
using System.Text;

namespace VSP.Tests.Drivers.RTSP;

/// <summary>
/// Minimal test-only loopback RTSP server: accepts a request, returns a scripted
/// response, optionally accepts one authenticated retry (as a new connection, matching
/// TcpRtspTransport's one-connection-per-request behavior), and can simulate a timeout
/// by holding the connection open without responding. Not a reusable mock-server framework.
/// </summary>
internal sealed class LoopbackRtspTestServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _stopSignal = new(initialState: false);

    public LoopbackRtspTestServer(string? firstResponse, string? secondResponse = null)
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        _thread = new Thread(() => Serve(firstResponse, secondResponse)) { IsBackground = true };
        _thread.Start();
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    private void Serve(string? firstResponse, string? secondResponse)
    {
        try
        {
            HandleOneConnection(firstResponse);

            if (secondResponse is not null)
            {
                HandleOneConnection(secondResponse);
            }
        }
        catch (Exception)
        {
            // Listener stopped or client disconnected; nothing to do in a test helper.
        }
    }

    private void HandleOneConnection(string? response)
    {
        using var client = _listener.AcceptTcpClient();
        using var stream = client.GetStream();

        var buffer = new byte[4096];
        _ = stream.Read(buffer);

        if (response is null)
        {
            // Hold the connection open (without responding) to simulate a hang,
            // until the test disposes the server.
            _stopSignal.Wait();
            return;
        }

        var responseBytes = Encoding.ASCII.GetBytes(response);
        stream.Write(responseBytes);
        stream.Flush();
    }

    public void Dispose()
    {
        _stopSignal.Set();
        _listener.Stop();
    }
}
