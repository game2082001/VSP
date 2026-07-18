namespace VSP.Device.Discovery.Rtsp;

public enum RtspProbeResponseClassification
{
    Success,
    AuthenticationRequired,
    NotFound,
    MethodNotAllowed,
    ProtocolNotSupported,
    InvalidResponse,
    ConnectionFailed,
    Timeout,
    UnknownFailure
}
