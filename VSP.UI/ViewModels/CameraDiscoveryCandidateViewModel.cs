using VSP.Core.MVVM;
using VSP.Device.Discovery.AutoDiscovery;
using VSP.Device.Discovery.Orchestration;
using VSP.Device.Drivers.Selection;

namespace VSP.UI.ViewModels;

public class CameraDiscoveryCandidateViewModel : ObservableObject
{
    public CandidateOrchestrationResult EvaluationResult { get; }

    public AutoDiscoveryCandidate Candidate { get; }

    public string Host => Candidate.Host ?? string.Empty;

    public string Sources => string.Join(", ", Candidate.Sources);

    public IReadOnlyList<DriverCompatibilityResult> DriverOptions =>
        EvaluationResult.DriverApprovalResult?.CompatibleDrivers ?? Array.Empty<DriverCompatibilityResult>();

    private string _name;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private DriverCompatibilityResult? _selectedDriver;
    public DriverCompatibilityResult? SelectedDriver
    {
        get => _selectedDriver;
        set
        {
            if (SetProperty(ref _selectedDriver, value))
            {
                OnPropertyChanged(nameof(CanRegister));
            }
        }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private bool _isRegistered;
    public bool IsRegistered
    {
        get => _isRegistered;
        set
        {
            if (SetProperty(ref _isRegistered, value))
            {
                OnPropertyChanged(nameof(CanRegister));
            }
        }
    }

    private string _statusMessage;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool CanRegister => !IsRegistered && SelectedDriver is not null;

    public CameraDiscoveryCandidateViewModel(CandidateOrchestrationResult evaluationResult)
    {
        ArgumentNullException.ThrowIfNull(evaluationResult);
        ArgumentNullException.ThrowIfNull(evaluationResult.Candidate);

        EvaluationResult = evaluationResult;
        Candidate = evaluationResult.Candidate;
        _name = BuildSuggestedName(Candidate);

        var approvedDriverId = evaluationResult.DriverApprovalResult?.ApprovedDriver?.DriverId;
        _selectedDriver = DriverOptions.FirstOrDefault(driver => driver.DriverId == approvedDriverId);

        _statusMessage = evaluationResult.Status switch
        {
            CandidateOrchestrationStatus.Approved => "Ready to register.",
            CandidateOrchestrationStatus.AwaitingApproval => "Choose a driver.",
            CandidateOrchestrationStatus.NoCompatibleDriver => "No compatible driver.",
            _ => evaluationResult.Reasons.Count > 0 ? evaluationResult.Reasons[0].Message : "Not evaluated."
        };
    }

    private static string BuildSuggestedName(AutoDiscoveryCandidate candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate.Name))
        {
            return candidate.Name;
        }

        if (!string.IsNullOrWhiteSpace(candidate.Host))
        {
            return candidate.Host;
        }

        if (!string.IsNullOrWhiteSpace(candidate.Endpoint))
        {
            return candidate.Endpoint;
        }

        return candidate.CandidateKey;
    }
}
