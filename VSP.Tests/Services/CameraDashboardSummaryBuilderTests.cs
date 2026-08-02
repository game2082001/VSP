using VSP.Device.Drivers;
using VSP.Device.Services;
using VSP.Domain.Enums;
using Xunit;
using EntityCamera = VSP.Domain.Entities.Camera;

namespace VSP.Tests.Services;

public class CameraDashboardSummaryBuilderTests
{
    [Fact]
    public void Build_WithNoCameras_ReturnsAllZeroesAndDoesNotThrow()
    {
        var summary = CameraDashboardSummaryBuilder.Build([], []);

        Assert.Equal(0, summary.TotalCameras);
        Assert.Equal(0, summary.OnlineCount);
        Assert.Equal(0, summary.OfflineCount);
        Assert.Equal(0, summary.UnknownStatusCount);
        Assert.Equal(0, summary.OnlineRatePercent);
        Assert.Empty(summary.ByConnectionType);
        Assert.Empty(summary.ByBrand);
        Assert.Empty(summary.RecentlyAdded);
        Assert.Empty(summary.RecentlyModified);
    }

    [Fact]
    public void Build_CountsStatusIntoOnlineOfflineAndUnknown()
    {
        var cameras = new[]
        {
            CreateCamera(status: CameraStatus.Online),
            CreateCamera(status: CameraStatus.Online),
            CreateCamera(status: CameraStatus.Offline),
            CreateCamera(status: CameraStatus.Connecting),
            CreateCamera(status: CameraStatus.Error)
        };

        var summary = CameraDashboardSummaryBuilder.Build(cameras, []);

        Assert.Equal(5, summary.TotalCameras);
        Assert.Equal(2, summary.OnlineCount);
        Assert.Equal(1, summary.OfflineCount);
        Assert.Equal(2, summary.UnknownStatusCount);
    }

    [Fact]
    public void Build_ComputesOnlineRatePercent()
    {
        var cameras = new[]
        {
            CreateCamera(status: CameraStatus.Online),
            CreateCamera(status: CameraStatus.Online),
            CreateCamera(status: CameraStatus.Online),
            CreateCamera(status: CameraStatus.Offline)
        };

        var summary = CameraDashboardSummaryBuilder.Build(cameras, []);

        Assert.Equal(75.0, summary.OnlineRatePercent);
    }

    [Fact]
    public void Build_GroupsByConnectionTypeAndBrand()
    {
        var cameras = new[]
        {
            CreateCamera(connectionType: DeviceConnectionType.RTSP, brand: CameraBrand.RTSP),
            CreateCamera(connectionType: DeviceConnectionType.RTSP, brand: CameraBrand.RTSP),
            CreateCamera(connectionType: DeviceConnectionType.ONVIF, brand: CameraBrand.ONVIF)
        };

        var summary = CameraDashboardSummaryBuilder.Build(cameras, []);

        Assert.Contains(summary.ByConnectionType, c => c.Category == "RTSP" && c.Count == 2);
        Assert.Contains(summary.ByConnectionType, c => c.Category == "ONVIF" && c.Count == 1);
        Assert.Contains(summary.ByBrand, c => c.Category == "RTSP" && c.Count == 2);
        Assert.Contains(summary.ByBrand, c => c.Category == "ONVIF" && c.Count == 1);
    }

    [Fact]
    public void Build_CountsImplementedVersusUnimplementedDriverCoverage()
    {
        var cameras = new[]
        {
            CreateCamera(connectionType: DeviceConnectionType.RTSP),
            CreateCamera(connectionType: DeviceConnectionType.ONVIF),
            CreateCamera(connectionType: DeviceConnectionType.HikvisionISAPI),
            CreateCamera(connectionType: DeviceConnectionType.DahuaNetSDK)
        };

        var summary = CameraDashboardSummaryBuilder.Build(cameras, []);

        Assert.Equal(2, summary.ImplementedDriverCameraCount);
        Assert.Equal(2, summary.UnimplementedDriverCameraCount);
    }

    [Fact]
    public void Build_RecentlyAdded_OrdersByCreateTimeDescendingAndLimitsToFive()
    {
        var cameras = Enumerable.Range(0, 8)
            .Select(i => CreateCamera(name: $"Camera{i}", createTime: new DateTime(2026, 1, 1).AddDays(i)))
            .ToArray();

        var summary = CameraDashboardSummaryBuilder.Build(cameras, []);

        Assert.Equal(5, summary.RecentlyAdded.Count);
        Assert.Equal("Camera7", summary.RecentlyAdded[0].Name);
        Assert.Equal("Camera3", summary.RecentlyAdded[4].Name);
    }

    [Fact]
    public void Build_RecentlyModified_OrdersByLastModifyTimeDescendingAndLimitsToFive()
    {
        var cameras = Enumerable.Range(0, 8)
            .Select(i => CreateCamera(name: $"Camera{i}", lastModifyTime: new DateTime(2026, 1, 1).AddDays(i)))
            .ToArray();

        var summary = CameraDashboardSummaryBuilder.Build(cameras, []);

        Assert.Equal(5, summary.RecentlyModified.Count);
        Assert.Equal("Camera7", summary.RecentlyModified[0].Name);
    }

    [Fact]
    public void Build_WithNullCameras_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CameraDashboardSummaryBuilder.Build(null!, []));
    }

    [Fact]
    public void Build_WithNullDrivers_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CameraDashboardSummaryBuilder.Build([], null!));
    }

    private static EntityCamera CreateCamera(
        string name = "Camera",
        CameraStatus status = CameraStatus.Offline,
        DeviceConnectionType connectionType = DeviceConnectionType.RTSP,
        CameraBrand brand = CameraBrand.RTSP,
        DateTime? createTime = null,
        DateTime? lastModifyTime = null)
    {
        return new EntityCamera
        {
            Name = name,
            Status = status,
            ConnectionType = connectionType,
            Brand = brand,
            CreateTime = createTime ?? new DateTime(2026, 1, 1),
            LastModifyTime = lastModifyTime ?? new DateTime(2026, 1, 1)
        };
    }
}
