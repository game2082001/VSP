using VSP.Device.Cameras.Factory;
using VSP.Device.Discovery.AutoDiscovery;
using VSP.Device.Drivers;
using VSP.Device.Drivers.Selection;
using VSP.Device.Registration;
using VSP.Domain.Enums;

namespace VSP.Device.Discovery.Orchestration;

public sealed class DiscoveryOrchestrator
{
    private readonly AutoDiscoveryCoordinator _autoDiscoveryCoordinator;
    private readonly AutoDiscoveryCandidateEvidenceMapper _evidenceMapper;
    private readonly DriverSelectionService _driverSelectionService;
    private readonly DriverRegistry _driverRegistry;
    private readonly IDriverApprovalPolicy _driverApprovalPolicy;
    private readonly CameraFactory _cameraFactory;
    private readonly DeviceRegistrationService _deviceRegistrationService;

    public DiscoveryOrchestrator(
        AutoDiscoveryCoordinator autoDiscoveryCoordinator,
        AutoDiscoveryCandidateEvidenceMapper evidenceMapper,
        DriverSelectionService driverSelectionService,
        DriverRegistry driverRegistry,
        IDriverApprovalPolicy driverApprovalPolicy,
        CameraFactory cameraFactory,
        DeviceRegistrationService deviceRegistrationService)
    {
        _autoDiscoveryCoordinator = autoDiscoveryCoordinator
            ?? throw new ArgumentNullException(nameof(autoDiscoveryCoordinator));
        _evidenceMapper = evidenceMapper
            ?? throw new ArgumentNullException(nameof(evidenceMapper));
        _driverSelectionService = driverSelectionService
            ?? throw new ArgumentNullException(nameof(driverSelectionService));
        _driverRegistry = driverRegistry
            ?? throw new ArgumentNullException(nameof(driverRegistry));
        _driverApprovalPolicy = driverApprovalPolicy
            ?? throw new ArgumentNullException(nameof(driverApprovalPolicy));
        _cameraFactory = cameraFactory
            ?? throw new ArgumentNullException(nameof(cameraFactory));
        _deviceRegistrationService = deviceRegistrationService
            ?? throw new ArgumentNullException(nameof(deviceRegistrationService));
    }

    public async Task<DiscoveryOrchestrationResult> ExecuteAsync(
        DiscoveryOrchestrationRequest? request,
        CancellationToken cancellationToken = default)
    {
        var candidateResults = new List<CandidateOrchestrationResult>();
        var validationReasons = ValidateRequest(request);
        if (validationReasons.Count > 0)
        {
            return new DiscoveryOrchestrationResult
            {
                Status = DiscoveryOrchestrationStatus.InvalidRequest,
                Reasons = validationReasons
            };
        }

        try
        {
            var candidates = await GetCandidatesAsync(request!, cancellationToken).ConfigureAwait(false);

            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                candidateResults.Add(ProcessCandidate(request!, candidate));
            }

            return CreateBatchResult(candidateResults);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new DiscoveryOrchestrationResult
            {
                Status = DiscoveryOrchestrationStatus.Cancelled,
                CandidateResults = candidateResults,
                Summary = CreateSummary(candidateResults),
                Reasons =
                [
                    new DiscoveryOrchestrationReason(
                        "Cancelled",
                        "Discovery orchestration was cancelled by the caller.")
                ]
            };
        }
        catch (Exception exception)
        {
            return new DiscoveryOrchestrationResult
            {
                Status = DiscoveryOrchestrationStatus.Failed,
                Reasons =
                [
                    new DiscoveryOrchestrationReason(
                        "OrchestrationFailure",
                        $"Discovery orchestration failed: {exception.Message}")
                ]
            };
        }
    }

    /// <summary>
    /// Discovery-only entry point: evaluates every candidate against driver selection and
    /// the configured <see cref="IDriverApprovalPolicy"/>, but never calls
    /// <see cref="CameraFactory"/> or <see cref="DeviceRegistrationService"/> — nothing is
    /// written to the repository. Reuses the exact same evaluation logic as
    /// <see cref="ExecuteAsync"/>/<see cref="ProcessCandidate"/>, just stopping one step earlier.
    /// A candidate evaluated here is later committed via <see cref="RegisterCandidate"/>.
    /// </summary>
    public async Task<IReadOnlyList<CandidateOrchestrationResult>> DiscoverCandidatesAsync(
        DiscoveryOrchestrationRequest? request,
        CancellationToken cancellationToken = default)
    {
        var validationReasons = ValidateRequest(request);
        if (validationReasons.Count > 0)
        {
            throw new ArgumentException(validationReasons[0].Message, nameof(request));
        }

        var candidates = await GetCandidatesAsync(request!, cancellationToken).ConfigureAwait(false);
        var results = new List<CandidateOrchestrationResult>();

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(EvaluateCandidate(request!, candidate));
        }

        return results;
    }

    /// <summary>
    /// Commits a single candidate that was already evaluated via <see cref="DiscoverCandidatesAsync"/>
    /// once a caller (typically an interactive reviewer) has chosen an approved driver and,
    /// optionally, an edited name. Reuses the exact same commit logic as
    /// <see cref="ExecuteAsync"/>/<see cref="ProcessCandidate"/> — <see cref="DeviceRegistrationService"/>
    /// remains the only place that writes a camera to the repository.
    /// </summary>
    public CandidateOrchestrationResult RegisterCandidate(
        AutoDiscoveryCandidate candidate,
        ApprovedDriverReference approvedDriver,
        DiscoveryOrchestrationRequest request,
        string? nameOverride = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(approvedDriver);
        ArgumentNullException.ThrowIfNull(request);

        return CommitCandidate(candidate, approvedDriver, request, nameOverride);
    }

    private async Task<IReadOnlyList<AutoDiscoveryCandidate>> GetCandidatesAsync(
        DiscoveryOrchestrationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Candidates is not null)
        {
            return request.Candidates;
        }

        var discoveryResult = await _autoDiscoveryCoordinator
            .DiscoverAsync(request.DiscoveryRequest, cancellationToken)
            .ConfigureAwait(false);

        return discoveryResult.Candidates;
    }

    private CandidateOrchestrationResult ProcessCandidate(
        DiscoveryOrchestrationRequest request,
        AutoDiscoveryCandidate candidate)
    {
        var evaluated = EvaluateCandidate(request, candidate);

        if (evaluated.Status != CandidateOrchestrationStatus.Approved
            || evaluated.DriverApprovalResult?.ApprovedDriver is null)
        {
            return evaluated;
        }

        return CommitCandidate(
            candidate,
            evaluated.DriverApprovalResult.ApprovedDriver,
            request,
            nameOverride: null,
            evaluated.DriverSelectionResult,
            evaluated.DriverApprovalResult);
    }

    private CandidateOrchestrationResult EvaluateCandidate(
        DiscoveryOrchestrationRequest request,
        AutoDiscoveryCandidate candidate)
    {
        var evidence = _evidenceMapper.Map(candidate);
        var driverSelectionResult = _driverSelectionService.Evaluate(new DriverSelectionRequest
        {
            Evidence = evidence,
            DriverDescriptors = _driverRegistry.GetAll()
        });
        var compatibleDrivers = driverSelectionResult.CompatibilityResults
            .Where(result => result.Status == DriverCompatibilityStatus.Compatible)
            .ToArray();
        var driverApprovalResult = _driverApprovalPolicy.Evaluate(new DriverApprovalRequest
        {
            Candidate = candidate,
            DriverSelectionResult = driverSelectionResult,
            CompatibleDrivers = compatibleDrivers,
            CorrelationId = request.CorrelationId
        });

        return new CandidateOrchestrationResult
        {
            Candidate = candidate,
            Status = MapApprovalStatus(driverApprovalResult.Status),
            DriverSelectionResult = driverSelectionResult,
            DriverApprovalResult = driverApprovalResult,
            Reasons = driverApprovalResult.Reasons
        };
    }

    private CandidateOrchestrationResult CommitCandidate(
        AutoDiscoveryCandidate candidate,
        ApprovedDriverReference approvedDriver,
        DiscoveryOrchestrationRequest request,
        string? nameOverride,
        DriverSelectionResult? driverSelectionResult = null,
        DriverApprovalResult? driverApprovalResult = null)
    {
        var cameraFactoryResult = _cameraFactory.Create(new CameraFactoryRequest
        {
            ApprovedDriver = approvedDriver,
            InitializationData = CreateInitializationData(candidate, nameOverride),
            Timestamp = request.Timestamp
        });

        if (cameraFactoryResult.Status != CameraFactoryStatus.Created
            || cameraFactoryResult.Camera is null)
        {
            return new CandidateOrchestrationResult
            {
                Candidate = candidate,
                Status = cameraFactoryResult.Status == CameraFactoryStatus.Rejected
                    ? CandidateOrchestrationStatus.CameraFactoryRejected
                    : CandidateOrchestrationStatus.Failed,
                DriverSelectionResult = driverSelectionResult,
                DriverApprovalResult = driverApprovalResult,
                CameraFactoryResult = cameraFactoryResult,
                Reasons = cameraFactoryResult.Reasons
                    .Select(reason => new DiscoveryOrchestrationReason(reason.Code, reason.Message))
                    .ToArray()
            };
        }

        var registrationResult = _deviceRegistrationService.Register(new DeviceRegistrationRequest
        {
            Camera = cameraFactoryResult.Camera,
            DuplicatePolicy = request.DuplicatePolicy,
            Source = request.RegistrationSource,
            CorrelationId = request.CorrelationId
        });

        return new CandidateOrchestrationResult
        {
            Candidate = candidate,
            Status = MapRegistrationStatus(registrationResult.Status),
            DriverSelectionResult = driverSelectionResult,
            DriverApprovalResult = driverApprovalResult,
            CameraFactoryResult = cameraFactoryResult,
            RegistrationResult = registrationResult,
            Reasons = registrationResult.Reasons
                .Select(reason => new DiscoveryOrchestrationReason(reason.Code, reason.Message))
                .ToArray()
        };
    }

    private static CameraInitializationData CreateInitializationData(
        AutoDiscoveryCandidate candidate,
        string? nameOverride = null)
    {
        var rtspUrl = IsRtspEndpoint(candidate.Endpoint) ? candidate.Endpoint : null;
        var port = candidate.Port;

        return new CameraInitializationData
        {
            Name = string.IsNullOrWhiteSpace(nameOverride)
                ? (string.IsNullOrWhiteSpace(candidate.Name)
                    ? FirstNonEmpty(candidate.Host, candidate.Endpoint, candidate.CandidateKey)
                    : candidate.Name)
                : nameOverride,
            IpAddress = candidate.Host,
            Brand = CameraBrand.Unknown,
            Model = candidate.Model,
            Location = candidate.Location,
            RtspPort = port,
            RtspUrl = rtspUrl
        };
    }

    private static DiscoveryOrchestrationResult CreateBatchResult(
        IReadOnlyList<CandidateOrchestrationResult> candidateResults)
    {
        var summary = CreateSummary(candidateResults);
        var hasErrors = candidateResults.Any(IsErrorStatus);

        return new DiscoveryOrchestrationResult
        {
            Status = hasErrors
                ? DiscoveryOrchestrationStatus.CompletedWithErrors
                : DiscoveryOrchestrationStatus.Completed,
            CandidateResults = candidateResults,
            Summary = summary,
            Reasons = hasErrors
                ?
                [
                    new DiscoveryOrchestrationReason(
                        "CompletedWithErrors",
                        "Discovery orchestration completed with one or more candidate-level issues.")
                ]
                : Array.Empty<DiscoveryOrchestrationReason>()
        };
    }

    private static DiscoveryOrchestrationSummary CreateSummary(
        IReadOnlyCollection<CandidateOrchestrationResult> candidateResults)
    {
        return new DiscoveryOrchestrationSummary
        {
            TotalCandidates = candidateResults.Count,
            Registered = candidateResults.Count(result => result.Status == CandidateOrchestrationStatus.Registered),
            Approved = candidateResults.Count(result => result.DriverApprovalResult?.Status == DriverApprovalStatus.Approved),
            AwaitingApproval = candidateResults.Count(result => result.Status == CandidateOrchestrationStatus.AwaitingApproval),
            DuplicateSkipped = candidateResults.Count(result => result.Status == CandidateOrchestrationStatus.DuplicateSkipped),
            Failed = candidateResults.Count(result => result.Status == CandidateOrchestrationStatus.Failed),
            Rejected = candidateResults.Count(result =>
                result.Status is CandidateOrchestrationStatus.NoCompatibleDriver
                    or CandidateOrchestrationStatus.CameraFactoryRejected
                    or CandidateOrchestrationStatus.RegistrationRejected
                    or CandidateOrchestrationStatus.Rejected)
        };
    }

    private static IReadOnlyList<DiscoveryOrchestrationReason> ValidateRequest(
        DiscoveryOrchestrationRequest? request)
    {
        if (request is null)
        {
            return
            [
                new DiscoveryOrchestrationReason(
                    "InvalidRequest",
                    "Discovery orchestration request is required.")
            ];
        }

        var hasDiscoveryRequest = request.DiscoveryRequest is not null;
        var hasCandidates = request.Candidates is not null;

        if (hasDiscoveryRequest == hasCandidates)
        {
            return
            [
                new DiscoveryOrchestrationReason(
                    "InvalidRequest",
                    "DiscoveryRequest and CandidateList must be provided exclusively.")
            ];
        }

        return Array.Empty<DiscoveryOrchestrationReason>();
    }

    private static CandidateOrchestrationStatus MapApprovalStatus(DriverApprovalStatus status)
    {
        return status switch
        {
            DriverApprovalStatus.Approved => CandidateOrchestrationStatus.Approved,
            DriverApprovalStatus.AwaitingApproval => CandidateOrchestrationStatus.AwaitingApproval,
            DriverApprovalStatus.NoCompatibleDriver => CandidateOrchestrationStatus.NoCompatibleDriver,
            DriverApprovalStatus.Rejected => CandidateOrchestrationStatus.Rejected,
            DriverApprovalStatus.Failed => CandidateOrchestrationStatus.Failed,
            _ => CandidateOrchestrationStatus.Failed
        };
    }

    private static CandidateOrchestrationStatus MapRegistrationStatus(DeviceRegistrationStatus status)
    {
        return status switch
        {
            DeviceRegistrationStatus.Registered => CandidateOrchestrationStatus.Registered,
            DeviceRegistrationStatus.SkippedDuplicate => CandidateOrchestrationStatus.DuplicateSkipped,
            DeviceRegistrationStatus.Rejected => CandidateOrchestrationStatus.RegistrationRejected,
            DeviceRegistrationStatus.Failed => CandidateOrchestrationStatus.Failed,
            _ => CandidateOrchestrationStatus.Failed
        };
    }

    private static bool IsErrorStatus(CandidateOrchestrationResult result)
    {
        return result.Status != CandidateOrchestrationStatus.Registered;
    }

    private static bool IsRtspEndpoint(string? endpoint)
    {
        return Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            && string.Equals(uri.Scheme, "rtsp", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
