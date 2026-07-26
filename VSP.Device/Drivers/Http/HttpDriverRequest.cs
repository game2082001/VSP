namespace VSP.Device.Drivers.Http;

public sealed class HttpDriverRequest
{
    public required Uri Endpoint { get; init; }

    public string Method { get; init; } = "POST";

    public string? ContentType { get; init; }

    public string? Body { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(3);
}
