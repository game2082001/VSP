using VSP.Domain.Entities;

namespace VSP.Device.Drivers.ONVIF;

/// <summary>
/// ONVIF-specific stream URI resolution (Device Service GetCapabilities -&gt; Media service
/// GetProfiles -&gt; GetStreamUri). Deliberately not part of <c>ICameraDriver</c> -- this
/// capability exists only for ONVIF and has no meaning for RTSP/Hikvision/Dahua, so it stays
/// out of the generic driver contract rather than forcing every driver to implement it.
/// </summary>
public interface IOnvifStreamUriResolver
{
    OnvifStreamUriResolution Resolve(Camera camera, CameraCredentials credentials) =>
        OnvifStreamUriResolution.Failure(
            OnvifStreamUriFailureReason.GetStreamUriFailed,
            "ONVIF stream URI resolution is not implemented.");
}
