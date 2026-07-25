namespace VSP.Device.Services;

public sealed class CameraConnectionTestResult
{
    public CameraConnectionTestResult(Guid cameraId, bool isSuccess)
    {
        CameraId = cameraId;
        IsSuccess = isSuccess;
    }

    public Guid CameraId { get; }

    public bool IsSuccess { get; }
}
