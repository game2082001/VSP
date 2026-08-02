namespace VSP.Device.Services;

/// <summary>
/// Dashboard Reality v1 (Epic-009): only fields backed by data that is actually persisted
/// and retrievable today. Deliberately excludes Discovery activity/history and registration
/// provenance (neither is ever persisted — see CameraDashboardSummaryBuilder) and
/// Camera.Recording (never set true by any production code path).
/// </summary>
public sealed class CameraDashboardSummary
{
    public int TotalCameras { get; init; }

    public int OnlineCount { get; init; }

    public int OfflineCount { get; init; }

    public int UnknownStatusCount { get; init; }

    public double OnlineRatePercent { get; init; }

    public IReadOnlyList<CameraCategoryCount> ByConnectionType { get; init; } = Array.Empty<CameraCategoryCount>();

    public IReadOnlyList<CameraCategoryCount> ByBrand { get; init; } = Array.Empty<CameraCategoryCount>();

    public int ImplementedDriverCameraCount { get; init; }

    public int UnimplementedDriverCameraCount { get; init; }

    public IReadOnlyList<CameraSummaryEntry> RecentlyAdded { get; init; } = Array.Empty<CameraSummaryEntry>();

    public IReadOnlyList<CameraSummaryEntry> RecentlyModified { get; init; } = Array.Empty<CameraSummaryEntry>();
}
