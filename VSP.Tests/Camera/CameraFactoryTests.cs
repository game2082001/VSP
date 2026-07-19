using VSP.Device.Cameras.Factory;
using VSP.Domain.Enums;
using Xunit;

namespace VSP.Tests.Camera;

public class CameraFactoryTests
{
    [Fact]
    public void Create_WithApprovedDriverAndInitializationData_CreatesCamera()
    {
        var timestamp = new DateTime(2026, 7, 19, 10, 30, 0);
        var factory = new CameraFactory();

        var result = factory.Create(new CameraFactoryRequest
        {
            ApprovedDriver = CreateApprovedDriver(DeviceConnectionType.ONVIF),
            InitializationData = new CameraInitializationData
            {
                Name = "Lobby",
                IpAddress = "192.168.1.10",
                Brand = CameraBrand.ONVIF,
                Model = "Model A",
                Location = "Front Desk",
                HttpPort = 8080,
                RtspPort = 8554,
                SdkPort = 9000
            },
            Timestamp = timestamp
        });

        Assert.Equal(CameraFactoryStatus.Created, result.Status);
        Assert.NotNull(result.Camera);
        Assert.Equal("Lobby", result.Camera!.Name);
        Assert.Equal("192.168.1.10", result.Camera.IpAddress);
        Assert.Equal(CameraBrand.ONVIF, result.Camera.Brand);
        Assert.Equal(DeviceConnectionType.ONVIF, result.Camera.ConnectionType);
        Assert.Equal("Model A", result.Camera.Model);
        Assert.Equal("Front Desk", result.Camera.Location);
        Assert.Equal(8080, result.Camera.HttpPort);
        Assert.Equal(8554, result.Camera.RtspPort);
        Assert.Equal(9000, result.Camera.SdkPort);
        Assert.Equal(CameraStatus.Offline, result.Camera.Status);
        Assert.Equal(timestamp, result.Camera.CreateTime);
        Assert.Equal(timestamp, result.Camera.LastModifyTime);
    }

    [Fact]
    public void Create_WithNullRequest_ReturnsRejectedResult()
    {
        var result = new CameraFactory().Create(null);

        AssertRejectedWithoutCamera(result, "MissingRequest");
    }

    [Fact]
    public void Create_WithMissingApprovedDriver_ReturnsRejectedResult()
    {
        var result = new CameraFactory().Create(new CameraFactoryRequest
        {
            InitializationData = CreateInitializationData()
        });

        AssertRejectedWithoutCamera(result, "MissingApprovedDriver");
    }

    [Fact]
    public void Create_WithUnapprovedDriver_ReturnsRejectedResult()
    {
        var result = new CameraFactory().Create(new CameraFactoryRequest
        {
            ApprovedDriver = CreateApprovedDriver(DeviceConnectionType.RTSP, isApproved: false),
            InitializationData = CreateInitializationData()
        });

        AssertRejectedWithoutCamera(result, "DriverNotApproved");
    }

    [Fact]
    public void Create_WithMissingName_ReturnsRejectedResult()
    {
        var result = new CameraFactory().Create(new CameraFactoryRequest
        {
            ApprovedDriver = CreateApprovedDriver(DeviceConnectionType.RTSP),
            InitializationData = CreateInitializationData(name: "")
        });

        AssertRejectedWithoutCamera(result, "MissingName");
    }

    [Fact]
    public void Create_WithMissingConnectionInformation_ReturnsRejectedResult()
    {
        var result = new CameraFactory().Create(new CameraFactoryRequest
        {
            ApprovedDriver = CreateApprovedDriver(DeviceConnectionType.RTSP),
            InitializationData = CreateInitializationData(ipAddress: "", rtspUrl: "")
        });

        AssertRejectedWithoutCamera(result, "MissingConnectionInformation");
    }

    [Theory]
    [InlineData(0, 554, 8000)]
    [InlineData(80, 0, 8000)]
    [InlineData(80, 554, 0)]
    [InlineData(65536, 554, 8000)]
    [InlineData(80, 65536, 8000)]
    [InlineData(80, 554, 65536)]
    public void Create_WithInvalidPorts_ReturnsRejectedResult(int httpPort, int rtspPort, int sdkPort)
    {
        var result = new CameraFactory().Create(new CameraFactoryRequest
        {
            ApprovedDriver = CreateApprovedDriver(DeviceConnectionType.RTSP),
            InitializationData = CreateInitializationData(
                httpPort: httpPort,
                rtspPort: rtspPort,
                sdkPort: sdkPort)
        });

        AssertRejectedWithoutCamera(result, "InvalidPort");
    }

    [Fact]
    public void Create_WithInvalidEndpoint_ReturnsRejectedResult()
    {
        var result = new CameraFactory().Create(new CameraFactoryRequest
        {
            ApprovedDriver = CreateApprovedDriver(DeviceConnectionType.RTSP),
            InitializationData = CreateInitializationData(rtspUrl: "http://192.168.1.10/stream1")
        });

        AssertRejectedWithoutCamera(result, "InvalidEndpoint");
    }

    [Fact]
    public void Create_PreservesExplicitRtspUrl()
    {
        var result = new CameraFactory().Create(new CameraFactoryRequest
        {
            ApprovedDriver = CreateApprovedDriver(DeviceConnectionType.RTSP),
            InitializationData = CreateInitializationData(rtspUrl: "rtsp://192.168.1.10/stream1")
        });

        Assert.Equal(CameraFactoryStatus.Created, result.Status);
        Assert.Equal("rtsp://192.168.1.10/stream1", result.Camera!.RtspUrl);
    }

    [Fact]
    public void Create_WithMissingBrand_DefaultsToUnknown()
    {
        var result = new CameraFactory().Create(new CameraFactoryRequest
        {
            ApprovedDriver = CreateApprovedDriver(DeviceConnectionType.RTSP),
            InitializationData = CreateInitializationData(brand: null)
        });

        Assert.Equal(CameraFactoryStatus.Created, result.Status);
        Assert.Equal(CameraBrand.Unknown, result.Camera!.Brand);
    }

    [Fact]
    public void Create_DoesNotInferBrandFromWeakEvidence()
    {
        var result = new CameraFactory().Create(new CameraFactoryRequest
        {
            ApprovedDriver = CreateApprovedDriver(
                DeviceConnectionType.HikvisionISAPI,
                driverDisplayName: "Hikvision Driver"),
            InitializationData = CreateInitializationData(
                brand: null,
                model: "Hikvision-like model")
        });

        Assert.Equal(CameraFactoryStatus.Created, result.Status);
        Assert.Equal(CameraBrand.Unknown, result.Camera!.Brand);
    }

    [Fact]
    public void Create_UsesConnectionTypeFromApprovedDriverReference()
    {
        var result = new CameraFactory().Create(new CameraFactoryRequest
        {
            ApprovedDriver = CreateApprovedDriver(DeviceConnectionType.ONVIF),
            InitializationData = CreateInitializationData()
        });

        Assert.Equal(CameraFactoryStatus.Created, result.Status);
        Assert.Equal(DeviceConnectionType.ONVIF, result.Camera!.ConnectionType);
    }

    [Fact]
    public void Create_DefaultsStatusWithoutConnectionTest()
    {
        var result = new CameraFactory().Create(new CameraFactoryRequest
        {
            ApprovedDriver = CreateApprovedDriver(DeviceConnectionType.RTSP),
            InitializationData = CreateInitializationData()
        });

        Assert.Equal(CameraFactoryStatus.Created, result.Status);
        Assert.Equal(CameraStatus.Offline, result.Camera!.Status);
    }

    [Fact]
    public void Create_InitializesCreateTimeAndLastModifyTimeConsistently()
    {
        var timestamp = new DateTime(2026, 7, 19, 12, 0, 0);

        var result = new CameraFactory().Create(new CameraFactoryRequest
        {
            ApprovedDriver = CreateApprovedDriver(DeviceConnectionType.RTSP),
            InitializationData = CreateInitializationData(),
            Timestamp = timestamp
        });

        Assert.Equal(CameraFactoryStatus.Created, result.Status);
        Assert.Equal(timestamp, result.Camera!.CreateTime);
        Assert.Equal(timestamp, result.Camera.LastModifyTime);
    }

    [Fact]
    public void Create_WithUnsupportedInitializationData_ReturnsRejectedResult()
    {
        var result = new CameraFactory().Create(new CameraFactoryRequest
        {
            ApprovedDriver = CreateApprovedDriver(DeviceConnectionType.RTSP),
            InitializationData = CreateInitializationData(
                unsupportedDataKeys: ["Credentials"])
        });

        AssertRejectedWithoutCamera(result, "UnsupportedInitializationData");
    }

    [Fact]
    public void CameraFactory_HasNoRepositoryOrSqliteDependency()
    {
        var exposedTypeNames = typeof(CameraFactory)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType.FullName ?? string.Empty))
            .Concat(typeof(CameraFactory).GetMethods().Select(method => method.ReturnType.FullName ?? string.Empty))
            .Concat(typeof(CameraFactory).GetFields().Select(field => field.FieldType.FullName ?? string.Empty));

        Assert.DoesNotContain(exposedTypeNames, typeName => typeName.Contains("Repository"));
        Assert.DoesNotContain(exposedTypeNames, typeName => typeName.Contains("SQLite"));
    }

    [Fact]
    public void CameraFactory_DoesNotDependOnDriverSelectionTypes()
    {
        var publicMethodParameterTypes = typeof(CameraFactory)
            .GetMethods()
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType.FullName ?? string.Empty);

        Assert.DoesNotContain(publicMethodParameterTypes, typeName => typeName.Contains(".Drivers.Selection."));
    }

    [Fact]
    public void CameraFactory_DoesNotInvokeDriverFactory()
    {
        var result = new CameraFactory().Create(new CameraFactoryRequest
        {
            ApprovedDriver = CreateApprovedDriver(DeviceConnectionType.RTSP),
            InitializationData = CreateInitializationData()
        });

        Assert.Equal(CameraFactoryStatus.Created, result.Status);
    }

    private static ApprovedDriverReference CreateApprovedDriver(
        DeviceConnectionType connectionType,
        bool isApproved = true,
        string driverDisplayName = "Test Driver")
    {
        return new ApprovedDriverReference
        {
            DriverId = "test.driver",
            ConnectionType = connectionType,
            IsApproved = isApproved,
            DriverDisplayName = driverDisplayName
        };
    }

    private static CameraInitializationData CreateInitializationData(
        string name = "Lobby",
        string ipAddress = "192.168.1.10",
        CameraBrand? brand = CameraBrand.RTSP,
        string model = "Model A",
        string? rtspUrl = null,
        int? httpPort = 80,
        int? rtspPort = 554,
        int? sdkPort = 8000,
        IReadOnlyCollection<string>? unsupportedDataKeys = null)
    {
        return new CameraInitializationData
        {
            Name = name,
            IpAddress = ipAddress,
            Brand = brand,
            Model = model,
            Location = "Front Desk",
            HttpPort = httpPort,
            RtspPort = rtspPort,
            SdkPort = sdkPort,
            RtspUrl = rtspUrl,
            UnsupportedDataKeys = unsupportedDataKeys ?? Array.Empty<string>()
        };
    }

    private static void AssertRejectedWithoutCamera(CameraFactoryResult result, string reasonCode)
    {
        Assert.Equal(CameraFactoryStatus.Rejected, result.Status);
        Assert.Null(result.Camera);
        Assert.Contains(result.Reasons, reason => reason.Code == reasonCode);
    }
}
