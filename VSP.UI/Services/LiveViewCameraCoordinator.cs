using VSP.Domain.Entities;

namespace VSP.UI.Services;

/// <summary>
/// Mediates "View Live" requests from the Camera Workspace to the Live View nav destination,
/// so camera selection originates from the Camera Workspace rather than internal Live View
/// discovery. CameraListViewModel publishes; MainWindowViewModel (composition root) is the
/// sole subscriber, forwarding the camera to LiveViewViewModel and switching navigation.
/// </summary>
public sealed class LiveViewCameraCoordinator
{
    public event Action<Camera>? CameraSelected;

    public void RequestLiveView(Camera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        CameraSelected?.Invoke(camera);
    }
}
