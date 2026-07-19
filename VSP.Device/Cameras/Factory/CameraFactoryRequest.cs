namespace VSP.Device.Cameras.Factory;

public sealed class CameraFactoryRequest
{
    public ApprovedDriverReference? ApprovedDriver { get; init; }

    public CameraInitializationData? InitializationData { get; init; }

    public DateTime? Timestamp { get; init; }
}
