using VSP.Device.Interfaces;
using VSP.Domain.Entities;

namespace VSP.Device.Registration;

public sealed class DeviceRegistrationService
{
    private readonly ICameraRepository _cameraRepository;

    public DeviceRegistrationService(ICameraRepository cameraRepository)
    {
        _cameraRepository = cameraRepository ?? throw new ArgumentNullException(nameof(cameraRepository));
    }

    public DeviceRegistrationResult Register(DeviceRegistrationRequest? request)
    {
        var validationReasons = ValidateRequest(request);
        if (validationReasons.Count > 0)
        {
            return Rejected(validationReasons);
        }

        try
        {
            var camera = request!.Camera!;
            var existingCameras = _cameraRepository.GetAll().ToArray();
            var duplicateResults = FindDuplicates(camera, existingCameras);

            if (duplicateResults.Count > 0)
            {
                return request.DuplicatePolicy == DuplicatePolicy.Skip
                    ? SkippedDuplicate(duplicateResults)
                    : Rejected(CreateDuplicateReasons(duplicateResults), duplicateResults);
            }

            _cameraRepository.Add(camera);

            return new DeviceRegistrationResult
            {
                Status = DeviceRegistrationStatus.Registered,
                RegisteredCameraId = camera.Id,
                Reasons =
                [
                    new DeviceRegistrationReason("Registered", "Camera was registered successfully.")
                ]
            };
        }
        catch (Exception exception)
        {
            return new DeviceRegistrationResult
            {
                Status = DeviceRegistrationStatus.Failed,
                Reasons =
                [
                    new DeviceRegistrationReason(
                        "RepositoryError",
                        $"Repository operation failed during device registration: {exception.Message}")
                ]
            };
        }
    }

    private static IReadOnlyList<DeviceRegistrationReason> ValidateRequest(DeviceRegistrationRequest? request)
    {
        var reasons = new List<DeviceRegistrationReason>();

        if (request is null)
        {
            reasons.Add(new DeviceRegistrationReason("MissingRequest", "Device registration request is required."));
            return reasons;
        }

        if (request.Camera is null)
        {
            reasons.Add(new DeviceRegistrationReason("MissingCamera", "Camera is required for registration."));
            return reasons;
        }

        if (string.IsNullOrWhiteSpace(request.Camera.Name))
        {
            reasons.Add(new DeviceRegistrationReason("MissingName", "Camera name is required for registration."));
        }

        if (string.IsNullOrWhiteSpace(request.Camera.IpAddress)
            && string.IsNullOrWhiteSpace(request.Camera.RtspUrl))
        {
            reasons.Add(new DeviceRegistrationReason(
                "MissingConnectionInformation",
                "Camera requires IP address or RTSP URL for registration."));
        }

        return reasons;
    }

    private static IReadOnlyList<DeviceDuplicateCheckResult> FindDuplicates(
        Camera camera,
        IEnumerable<Camera> existingCameras)
    {
        var duplicates = new List<DeviceDuplicateCheckResult>();

        foreach (var existingCamera in existingCameras)
        {
            if (existingCamera.Id == camera.Id)
            {
                continue;
            }

            AddDuplicateIfMatches(
                duplicates,
                "Name",
                "DuplicateName",
                "Camera name duplicates an existing camera.",
                camera.Name,
                existingCamera.Name,
                existingCamera.Id);
            AddDuplicateIfMatches(
                duplicates,
                "IpAddress",
                "DuplicateIpAddress",
                "Camera IP address duplicates an existing camera.",
                camera.IpAddress,
                existingCamera.IpAddress,
                existingCamera.Id);

            if (!string.IsNullOrWhiteSpace(camera.RtspUrl)
                && !string.IsNullOrWhiteSpace(existingCamera.RtspUrl))
            {
                AddDuplicateIfMatches(
                    duplicates,
                    "RtspUrl",
                    "DuplicateRtspUrl",
                    "Camera RTSP URL duplicates an existing camera.",
                    camera.RtspUrl,
                    existingCamera.RtspUrl,
                    existingCamera.Id);
            }
        }

        return duplicates;
    }

    private static void AddDuplicateIfMatches(
        ICollection<DeviceDuplicateCheckResult> duplicates,
        string fieldName,
        string code,
        string message,
        string candidateValue,
        string existingValue,
        Guid existingCameraId)
    {
        if (string.IsNullOrWhiteSpace(candidateValue)
            || string.IsNullOrWhiteSpace(existingValue))
        {
            return;
        }

        if (string.Equals(candidateValue.Trim(), existingValue.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            duplicates.Add(new DeviceDuplicateCheckResult(fieldName, code, message, existingCameraId));
        }
    }

    private static IReadOnlyList<DeviceRegistrationReason> CreateDuplicateReasons(
        IEnumerable<DeviceDuplicateCheckResult> duplicateResults)
    {
        return duplicateResults
            .Select(duplicate => new DeviceRegistrationReason(duplicate.Code, duplicate.Message))
            .ToArray();
    }

    private static DeviceRegistrationResult Rejected(
        IReadOnlyList<DeviceRegistrationReason> reasons,
        IReadOnlyList<DeviceDuplicateCheckResult>? duplicateResults = null)
    {
        return new DeviceRegistrationResult
        {
            Status = DeviceRegistrationStatus.Rejected,
            DuplicateResults = duplicateResults ?? Array.Empty<DeviceDuplicateCheckResult>(),
            Reasons = reasons
        };
    }

    private static DeviceRegistrationResult SkippedDuplicate(
        IReadOnlyList<DeviceDuplicateCheckResult> duplicateResults)
    {
        return new DeviceRegistrationResult
        {
            Status = DeviceRegistrationStatus.SkippedDuplicate,
            DuplicateResults = duplicateResults,
            Reasons =
            [
                new DeviceRegistrationReason("SkippedDuplicate", "Camera registration was skipped because a duplicate was found.")
            ]
        };
    }
}
