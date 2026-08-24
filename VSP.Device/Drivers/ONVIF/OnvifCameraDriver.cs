using VSP.Core.Logging;
using VSP.Device.Drivers.Abstractions;
using VSP.Device.Drivers.Http;
using VSP.Domain.Entities;

namespace VSP.Device.Drivers.ONVIF;

public class OnvifCameraDriver : ICameraDriver
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);
    private static readonly OnvifDeviceManagementRequestFactory RequestFactory = new();
    private static readonly OnvifDeviceManagementResponseParser ResponseParser = new();

    public string DriverId => "generic.onvif";

    public string DisplayName => "Generic ONVIF Driver";

    public DeviceCapability Capability { get; } = new()
    {
        SupportsLiveView = true,
        SupportsSnapshot = true,
        SupportsPlayback = false,
        SupportsPTZ = true,
        SupportsAudio = false,
        SupportsEvent = false,
        SupportsDiscovery = true,
        SupportsDeviceInformation = true
    };

    public bool TestConnection(Camera camera, CameraCredentials credentials)
    {
        if (string.IsNullOrWhiteSpace(camera.IpAddress))
        {
            return false;
        }

        try
        {
            var response = HttpDriverTransport.Send(new HttpDriverRequest
            {
                Endpoint = BuildDeviceServiceEndpoint(camera),
                ContentType = "application/soap+xml",
                Body = RequestFactory.BuildGetSystemDateAndTimeRequest(),
                Timeout = RequestTimeout
            });

            return IsSuccess(response.StatusCode) && ResponseParser.IsSuccessfulResponse(response.Body);
        }
        catch (Exception ex)
        {
            AppLog.Warning($"ONVIF TestConnection failed for camera at {camera.IpAddress}:{camera.HttpPort}.", ex);
            return false;
        }
    }

    public DeviceInformation? GetDeviceInformation(Camera camera, CameraCredentials credentials)
    {
        if (string.IsNullOrWhiteSpace(camera.IpAddress))
        {
            return null;
        }

        try
        {
            var response = HttpDriverTransport.Send(new HttpDriverRequest
            {
                Endpoint = BuildDeviceServiceEndpoint(camera),
                ContentType = "application/soap+xml",
                Body = RequestFactory.BuildGetDeviceInformationRequest(credentials.Username, credentials.Password),
                Timeout = RequestTimeout
            });

            return IsSuccess(response.StatusCode) ? ResponseParser.ParseDeviceInformation(response.Body) : null;
        }
        catch (Exception ex)
        {
            AppLog.Warning($"ONVIF GetDeviceInformation failed for camera at {camera.IpAddress}:{camera.HttpPort}.", ex);
            return null;
        }
    }

    public bool StartLive(Camera camera)
    {
        return false;
    }

    public bool StopLive(Camera camera)
    {
        return false;
    }

    public bool Snapshot(Camera camera)
    {
        return false;
    }

    private static Uri BuildDeviceServiceEndpoint(Camera camera)
    {
        var port = camera.HttpPort > 0 ? camera.HttpPort : 80;
        return new Uri($"http://{camera.IpAddress}:{port}/onvif/device_service");
    }

    private static bool IsSuccess(int statusCode)
    {
        return statusCode is >= 200 and < 300;
    }
}
