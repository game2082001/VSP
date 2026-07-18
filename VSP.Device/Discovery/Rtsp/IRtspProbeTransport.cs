namespace VSP.Device.Discovery.Rtsp;

public interface IRtspProbeTransport
{
    Task<string> SendAndReceiveAsync(
        string requestPayload,
        string host,
        int port,
        CancellationToken cancellationToken);
}
