using System.Net.Sockets;
using System.Text;

namespace VSP.Device.Discovery.Rtsp;

public sealed class TcpRtspProbeTransport : IRtspProbeTransport
{
    private const int MaxResponseBytes = 16 * 1024;

    public async Task<string> SendAndReceiveAsync(
        string requestPayload,
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestPayload);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(host, port, cancellationToken);

        await using var stream = tcpClient.GetStream();

        var requestBytes = Encoding.ASCII.GetBytes(requestPayload);
        await stream.WriteAsync(requestBytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);

        var responseBuffer = new byte[1024];
        var responseBytes = new List<byte>();

        while (responseBytes.Count < MaxResponseBytes)
        {
            var bytesRead = await stream.ReadAsync(responseBuffer, cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            responseBytes.AddRange(responseBuffer.AsSpan(0, bytesRead).ToArray());

            if (HasCompleteHeaders(responseBytes))
            {
                break;
            }
        }

        return Encoding.ASCII.GetString(responseBytes.ToArray());
    }

    private static bool HasCompleteHeaders(IReadOnlyList<byte> responseBytes)
    {
        if (responseBytes.Count < 4)
        {
            return false;
        }

        for (var i = 0; i <= responseBytes.Count - 4; i++)
        {
            if (responseBytes[i] == '\r'
                && responseBytes[i + 1] == '\n'
                && responseBytes[i + 2] == '\r'
                && responseBytes[i + 3] == '\n')
            {
                return true;
            }
        }

        return false;
    }
}
