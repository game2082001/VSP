using System.Text;
using VSP.Domain.Entities;

namespace VSP.Device.Export;

public static class CameraExportWriter
{
    private static readonly string[] Headers =
    {
        "Name",
        "Brand",
        "Model",
        "IP Address",
        "HTTP Port",
        "RTSP Port",
        "SDK Port",
        "Username",
        "Password",
        "Connection Type",
        "RTSP URL",
        "Location"
    };

    public static string Write(IReadOnlyList<Camera> cameras)
    {
        ArgumentNullException.ThrowIfNull(cameras);

        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', Headers));

        foreach (var camera in cameras)
        {
            var fields = new[]
            {
                camera.Name,
                camera.Brand.ToString(),
                camera.Model,
                camera.IpAddress,
                camera.HttpPort.ToString(),
                camera.RtspPort.ToString(),
                camera.SdkPort.ToString(),
                camera.Username,
                camera.Password,
                camera.ConnectionType.ToString(),
                camera.RtspUrl,
                camera.Location
            };

            builder.AppendLine(string.Join(',', fields.Select(EscapeField)));
        }

        return builder.ToString();
    }

    private static string EscapeField(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
