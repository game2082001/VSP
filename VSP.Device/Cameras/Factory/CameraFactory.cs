using VSP.Domain.Entities;
using VSP.Domain.Enums;

namespace VSP.Device.Cameras.Factory;

public sealed class CameraFactory
{
    public CameraFactoryResult Create(CameraFactoryRequest? request)
    {
        try
        {
            var validationReasons = Validate(request);
            if (validationReasons.Count > 0)
            {
                return Rejected(validationReasons);
            }

            var approvedDriver = request!.ApprovedDriver!;
            var initializationData = request.InitializationData!;
            var timestamp = request.Timestamp ?? DateTime.Now;

            var camera = new Camera
            {
                Name = initializationData.Name!.Trim(),
                IpAddress = Normalize(initializationData.IpAddress),
                Brand = initializationData.Brand ?? CameraBrand.Unknown,
                ConnectionType = approvedDriver.ConnectionType,
                Model = Normalize(initializationData.Model),
                Location = Normalize(initializationData.Location),
                HttpPort = initializationData.HttpPort ?? 80,
                RtspPort = initializationData.RtspPort ?? 554,
                SdkPort = initializationData.SdkPort ?? 8000,
                RtspUrl = Normalize(initializationData.RtspUrl),
                Status = CameraStatus.Offline,
                CreateTime = timestamp,
                LastModifyTime = timestamp
            };

            return new CameraFactoryResult
            {
                Status = CameraFactoryStatus.Created,
                Camera = camera,
                Reasons =
                [
                    new CameraFactoryReason("CameraCreated", "Camera entity was created.")
                ]
            };
        }
        catch (Exception exception)
        {
            return new CameraFactoryResult
            {
                Status = CameraFactoryStatus.Failed,
                Camera = null,
                Reasons =
                [
                    new CameraFactoryReason(
                        "UnexpectedFactoryFailure",
                        $"Unexpected camera factory failure: {exception.Message}")
                ]
            };
        }
    }

    private static IReadOnlyList<CameraFactoryReason> Validate(CameraFactoryRequest? request)
    {
        var reasons = new List<CameraFactoryReason>();

        if (request is null)
        {
            reasons.Add(new CameraFactoryReason("MissingRequest", "Camera factory request is required."));
            return reasons;
        }

        ValidateApprovedDriver(request.ApprovedDriver, reasons);
        ValidateInitializationData(request.InitializationData, reasons);

        return reasons;
    }

    private static void ValidateApprovedDriver(
        ApprovedDriverReference? approvedDriver,
        ICollection<CameraFactoryReason> reasons)
    {
        if (approvedDriver is null)
        {
            reasons.Add(new CameraFactoryReason("MissingApprovedDriver", "Approved driver reference is required."));
            return;
        }

        if (!approvedDriver.IsApproved)
        {
            reasons.Add(new CameraFactoryReason("DriverNotApproved", "Driver reference is not approved."));
        }

        if (string.IsNullOrWhiteSpace(approvedDriver.DriverId)
            || approvedDriver.ConnectionType == DeviceConnectionType.Unknown)
        {
            reasons.Add(new CameraFactoryReason(
                "MissingApprovedDriver",
                "Approved driver reference must include driver id and connection type."));
        }
    }

    private static void ValidateInitializationData(
        CameraInitializationData? initializationData,
        ICollection<CameraFactoryReason> reasons)
    {
        if (initializationData is null)
        {
            reasons.Add(new CameraFactoryReason(
                "MissingConnectionInformation",
                "Camera initialization data is required."));
            return;
        }

        if (string.IsNullOrWhiteSpace(initializationData.Name))
        {
            reasons.Add(new CameraFactoryReason("MissingName", "Camera name is required."));
        }

        if (string.IsNullOrWhiteSpace(initializationData.IpAddress)
            && string.IsNullOrWhiteSpace(initializationData.RtspUrl))
        {
            reasons.Add(new CameraFactoryReason(
                "MissingConnectionInformation",
                "Camera requires IP address or explicit RTSP URL."));
        }

        ValidatePort(initializationData.HttpPort, nameof(initializationData.HttpPort), reasons);
        ValidatePort(initializationData.RtspPort, nameof(initializationData.RtspPort), reasons);
        ValidatePort(initializationData.SdkPort, nameof(initializationData.SdkPort), reasons);
        ValidateRtspUrl(initializationData.RtspUrl, reasons);

        foreach (var unsupportedKey in initializationData.UnsupportedDataKeys)
        {
            reasons.Add(new CameraFactoryReason(
                "UnsupportedInitializationData",
                $"Unsupported initialization data was provided: {unsupportedKey}."));
        }
    }

    private static void ValidatePort(
        int? port,
        string fieldName,
        ICollection<CameraFactoryReason> reasons)
    {
        if (port is null)
        {
            return;
        }

        if (port is < 1 or > 65535)
        {
            reasons.Add(new CameraFactoryReason(
                "InvalidPort",
                $"{fieldName} must be between 1 and 65535."));
        }
    }

    private static void ValidateRtspUrl(string? rtspUrl, ICollection<CameraFactoryReason> reasons)
    {
        if (string.IsNullOrWhiteSpace(rtspUrl))
        {
            return;
        }

        if (!Uri.TryCreate(rtspUrl, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, "rtsp", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add(new CameraFactoryReason(
                "InvalidEndpoint",
                "RTSP URL must be an absolute rtsp endpoint."));
        }
    }

    private static CameraFactoryResult Rejected(IReadOnlyList<CameraFactoryReason> reasons)
    {
        return new CameraFactoryResult
        {
            Status = CameraFactoryStatus.Rejected,
            Camera = null,
            Reasons = reasons
        };
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
