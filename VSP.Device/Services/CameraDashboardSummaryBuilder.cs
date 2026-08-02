using VSP.Device.Drivers;
using VSP.Domain.Entities;
using VSP.Domain.Enums;

namespace VSP.Device.Services;

/// <summary>
/// Pure aggregation over already-loaded Camera and driver data - no I/O, no repository
/// access of its own. Deliberately does not compute anything about Discovery activity or
/// registration provenance: neither is ever persisted (see Epic-009 Current-State Analysis),
/// so no field here claims to represent it.
/// </summary>
public static class CameraDashboardSummaryBuilder
{
    private const int RecentItemCount = 5;

    public static CameraDashboardSummary Build(
        IReadOnlyList<Camera> cameras,
        IReadOnlyList<DriverDescriptor> driverDescriptors)
    {
        ArgumentNullException.ThrowIfNull(cameras);
        ArgumentNullException.ThrowIfNull(driverDescriptors);

        var total = cameras.Count;
        var online = cameras.Count(camera => camera.Status == CameraStatus.Online);
        var offline = cameras.Count(camera => camera.Status == CameraStatus.Offline);
        var unknown = total - online - offline;
        var implementedCount = cameras.Count(camera => DriverFactory.IsDriverImplemented(camera.ConnectionType));

        return new CameraDashboardSummary
        {
            TotalCameras = total,
            OnlineCount = online,
            OfflineCount = offline,
            UnknownStatusCount = unknown,
            OnlineRatePercent = total == 0 ? 0 : Math.Round(online * 100.0 / total, 1),
            ByConnectionType = GroupCount(cameras, camera => camera.ConnectionType.ToString()),
            ByBrand = GroupCount(cameras, camera => camera.Brand.ToString()),
            ImplementedDriverCameraCount = implementedCount,
            UnimplementedDriverCameraCount = total - implementedCount,
            RecentlyAdded = cameras
                .OrderByDescending(camera => camera.CreateTime)
                .Take(RecentItemCount)
                .Select(camera => ToEntry(camera, camera.CreateTime))
                .ToArray(),
            RecentlyModified = cameras
                .OrderByDescending(camera => camera.LastModifyTime)
                .Take(RecentItemCount)
                .Select(camera => ToEntry(camera, camera.LastModifyTime))
                .ToArray()
        };
    }

    private static IReadOnlyList<CameraCategoryCount> GroupCount(
        IReadOnlyList<Camera> cameras,
        Func<Camera, string> categorySelector)
    {
        return cameras
            .GroupBy(categorySelector)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new CameraCategoryCount { Category = group.Key, Count = group.Count() })
            .ToArray();
    }

    private static CameraSummaryEntry ToEntry(Camera camera, DateTime timestamp)
    {
        return new CameraSummaryEntry
        {
            CameraId = camera.Id,
            Name = camera.Name,
            IpAddress = camera.IpAddress,
            Timestamp = timestamp
        };
    }
}
