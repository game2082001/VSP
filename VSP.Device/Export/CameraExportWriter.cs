using System.Text;
using VSP.Device.Import;
using VSP.Domain.Entities;

namespace VSP.Device.Export;

public static class CameraExportWriter
{
    public static string Write(IReadOnlyList<Camera> cameras)
    {
        ArgumentNullException.ThrowIfNull(cameras);

        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', CameraImportHeaders.AllowedHeaders));

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
