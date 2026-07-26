namespace VSP.Device.Drivers.Http;

public sealed class HttpDriverResponse
{
    public int StatusCode { get; init; }

    public string Body { get; init; } = string.Empty;
}
