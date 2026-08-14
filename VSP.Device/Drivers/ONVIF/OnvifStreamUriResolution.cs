namespace VSP.Device.Drivers.ONVIF;

public enum OnvifStreamUriFailureReason
{
    DeviceServiceUnavailable,
    MediaServiceUnavailable,
    AuthenticationFailed,
    NoMediaProfiles,
    GetStreamUriFailed,
    InvalidStreamUri
}

/// <summary>
/// Outcome of <see cref="IOnvifStreamUriResolver.Resolve"/>. Never carries credentials or
/// Authorization/SOAP-security header values -- <see cref="FailureMessage"/> is always a
/// fixed, human-readable string per <see cref="FailureReason"/>, not raw response content.
/// </summary>
public sealed class OnvifStreamUriResolution
{
    private OnvifStreamUriResolution(
        bool isSuccess,
        string? streamUri,
        OnvifStreamUriFailureReason? failureReason,
        string? failureMessage)
    {
        IsSuccess = isSuccess;
        StreamUri = streamUri;
        FailureReason = failureReason;
        FailureMessage = failureMessage;
    }

    public bool IsSuccess { get; }

    public string? StreamUri { get; }

    public OnvifStreamUriFailureReason? FailureReason { get; }

    public string? FailureMessage { get; }

    public static OnvifStreamUriResolution Success(string streamUri)
    {
        return new OnvifStreamUriResolution(true, streamUri, null, null);
    }

    public static OnvifStreamUriResolution Failure(OnvifStreamUriFailureReason reason, string message)
    {
        return new OnvifStreamUriResolution(false, null, reason, message);
    }
}
