namespace VSP.Device.Import;

public static class CameraImportHeaders
{
    public const string Name = "Name";
    public const string Brand = "Brand";
    public const string Model = "Model";
    public const string IpAddress = "IP Address";
    public const string HttpPort = "HTTP Port";
    public const string RtspPort = "RTSP Port";
    public const string SdkPort = "SDK Port";
    public const string Username = "Username";
    public const string Password = "Password";
    public const string ConnectionType = "Connection Type";
    public const string RtspUrl = "RTSP URL";
    public const string Location = "Location";

    public static readonly string[] AllowedHeaders =
    {
        Name,
        Brand,
        Model,
        IpAddress,
        HttpPort,
        RtspPort,
        SdkPort,
        Username,
        ConnectionType,
        RtspUrl,
        Location
    };

    public static string Normalize(string header)
    {
        var trimmedHeader = header.Trim();
        var matchedHeader = AllowedHeaders
            .Append(Password)
            .FirstOrDefault(x => string.Equals(x, trimmedHeader, StringComparison.OrdinalIgnoreCase));

        return matchedHeader ?? trimmedHeader;
    }

    public static void RejectSecretHeaders(IEnumerable<string> headers)
    {
        if (headers.Any(header => string.Equals(header.Trim(), Password, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("Import file contains a prohibited credential column.");
        }
    }
}
