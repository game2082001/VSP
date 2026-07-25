using System.Net.Sockets;
using System.Text;

namespace VSP.Device.Drivers.RTSP;

public static class TcpRtspTransport
{
    private const int MaxResponseBytes = 16 * 1024;

    public static string SendAndReceive(
        string requestPayload,
        string host,
        int port,
        TimeSpan connectTimeout,
        TimeSpan readTimeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestPayload);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        using var tcpClient = new TcpClient();
        Connect(tcpClient, host, port, connectTimeout);

        using var stream = tcpClient.GetStream();
        stream.ReadTimeout = (int)readTimeout.TotalMilliseconds;
        stream.WriteTimeout = (int)readTimeout.TotalMilliseconds;

        var requestBytes = Encoding.ASCII.GetBytes(requestPayload);
        stream.Write(requestBytes, 0, requestBytes.Length);
        stream.Flush();

        return ReadResponse(stream);
    }

    private static void Connect(TcpClient tcpClient, string host, int port, TimeSpan connectTimeout)
    {
        using var cts = new CancellationTokenSource(connectTimeout);

        try
        {
            tcpClient.ConnectAsync(host, port, cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException ex)
        {
            throw new TimeoutException("The RTSP connection attempt timed out.", ex);
        }
    }

    private static string ReadResponse(NetworkStream stream)
    {
        var responseBuffer = new byte[1024];
        var responseBytes = new List<byte>();

        while (responseBytes.Count < MaxResponseBytes)
        {
            int bytesRead;
            try
            {
                bytesRead = stream.Read(responseBuffer, 0, responseBuffer.Length);
            }
            catch (IOException ex) when (ex.InnerException is SocketException { SocketErrorCode: SocketError.TimedOut })
            {
                throw new TimeoutException("The RTSP read operation timed out.", ex);
            }

            if (bytesRead == 0)
            {
                break;
            }

            for (var i = 0; i < bytesRead; i++)
            {
                responseBytes.Add(responseBuffer[i]);
            }

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
