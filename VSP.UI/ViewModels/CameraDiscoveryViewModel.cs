using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using VSP.Core.Commands;
using VSP.Core.MVVM;
using VSP.Device.Cameras.Factory;
using VSP.Device.Discovery.AutoDiscovery;
using VSP.Device.Discovery.NetworkScan;
using VSP.Device.Discovery.Orchestration;
using VSP.Device.Discovery.Rtsp;
using VSP.Device.Discovery.Workspace;
using VSP.Device.Registration;

namespace VSP.UI.ViewModels;

public class CameraDiscoveryViewModel : ObservableObject
{
    private readonly DiscoveryOrchestrator _orchestrator;

    private CancellationTokenSource? _scanCancellationSource;

    public ObservableCollection<CameraDiscoveryCandidateViewModel> Candidates { get; } = new();

    private bool _enableOnvifDiscovery = true;
    public bool EnableOnvifDiscovery
    {
        get => _enableOnvifDiscovery;
        set => SetProperty(ref _enableOnvifDiscovery, value);
    }

    private bool _enableNetworkScan;
    public bool EnableNetworkScan
    {
        get => _enableNetworkScan;
        set => SetProperty(ref _enableNetworkScan, value);
    }

    private bool _enableRtspEndpointProbe;
    public bool EnableRtspEndpointProbe
    {
        get => _enableRtspEndpointProbe;
        set => SetProperty(ref _enableRtspEndpointProbe, value);
    }

    private string _targetHostsText = string.Empty;
    public string TargetHostsText
    {
        get => _targetHostsText;
        set => SetProperty(ref _targetHostsText, value);
    }

    private int _targetPort = 80;
    public int TargetPort
    {
        get => _targetPort;
        set => SetProperty(ref _targetPort, value);
    }

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                RaiseCommandStateChanged();
            }
        }
    }

    private string _statusMessage = "Ready.";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand ScanCommand { get; }
    public ICommand CancelScanCommand { get; }
    public ICommand RegisterSelectedCommand { get; }

    public CameraDiscoveryViewModel(DiscoveryOrchestrator orchestrator)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));

        ScanCommand = new RelayCommand(() => _ = ScanAsync(), () => !IsScanning);
        CancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);
        RegisterSelectedCommand = new RelayCommand(
            () => _ = RegisterSelectedAsync(),
            () => !IsScanning && Candidates.Any(candidate => candidate.IsSelected));
    }

    public async Task ScanAsync()
    {
        _scanCancellationSource?.Dispose();
        _scanCancellationSource = new CancellationTokenSource();

        IsScanning = true;
        StatusMessage = "Scanning...";

        try
        {
            var request = BuildDiscoveryRequest();
            var evaluations = await _orchestrator
                .DiscoverCandidatesAsync(request, _scanCancellationSource.Token);

            foreach (var candidate in Candidates)
            {
                candidate.PropertyChanged -= HandleCandidatePropertyChanged;
            }

            Candidates.Clear();
            foreach (var evaluation in evaluations)
            {
                var candidate = new CameraDiscoveryCandidateViewModel(evaluation);
                candidate.PropertyChanged += HandleCandidatePropertyChanged;
                Candidates.Add(candidate);
            }

            StatusMessage = Candidates.Count == 0
                ? "Scan complete. No candidates found."
                : $"Scan complete. {Candidates.Count} candidate(s) found.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Scan cancelled.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Scan failed: {exception.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    public async Task RegisterSelectedAsync()
    {
        var selected = Candidates.Where(candidate => candidate.IsSelected).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var request = BuildRegistrationRequest();
        var registeredCount = 0;

        foreach (var candidate in selected)
        {
            if (!candidate.CanRegister)
            {
                candidate.StatusMessage = "Choose a compatible driver before registering.";
                continue;
            }

            var approvedDriver = new ApprovedDriverReference
            {
                DriverId = candidate.SelectedDriver!.DriverId,
                ConnectionType = candidate.SelectedDriver.ConnectionType,
                DriverDisplayName = candidate.SelectedDriver.DisplayName,
                IsApproved = true
            };

            var result = _orchestrator.RegisterCandidate(
                candidate.Candidate,
                approvedDriver,
                request,
                candidate.Name);

            candidate.StatusMessage = DescribeResult(result);
            candidate.IsRegistered = result.Status == CandidateOrchestrationStatus.Registered;

            if (candidate.IsRegistered)
            {
                registeredCount++;
                candidate.IsSelected = false;
            }
        }

        await Task.CompletedTask;

        StatusMessage = $"Registered {registeredCount} of {selected.Count} selected candidate(s).";
    }

    private DiscoveryOrchestrationRequest BuildDiscoveryRequest()
    {
        var targets = NetworkScanTargetParser.Parse(TargetHostsText, TargetPort);
        var hasTargets = targets.Count > 0;

        var autoDiscoveryRequest = new AutoDiscoveryRequest
        {
            EnableOnvifDiscovery = EnableOnvifDiscovery,
            EnableNetworkScan = EnableNetworkScan && hasTargets,
            NetworkScanRequest = hasTargets ? new NetworkScanRequest { Targets = targets } : null,
            EnableRtspEndpointProbe = EnableRtspEndpointProbe && hasTargets,
            RtspEndpointProbeRequests = EnableRtspEndpointProbe && hasTargets
                ? targets.Select(target => new RtspEndpointProbeRequest
                {
                    Host = target.Host,
                    Port = target.Port ?? TargetPort
                }).ToArray()
                : Array.Empty<RtspEndpointProbeRequest>()
        };

        return new DiscoveryOrchestrationRequest
        {
            DiscoveryRequest = autoDiscoveryRequest
        };
    }

    private static DiscoveryOrchestrationRequest BuildRegistrationRequest()
    {
        return new DiscoveryOrchestrationRequest
        {
            DuplicatePolicy = DuplicatePolicy.Skip,
            RegistrationSource = RegistrationSource.Discovery,
            Timestamp = DateTime.Now
        };
    }

    private void CancelScan()
    {
        _scanCancellationSource?.Cancel();
    }

    private void HandleCandidatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CameraDiscoveryCandidateViewModel.IsSelected))
        {
            (RegisterSelectedCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private void RaiseCommandStateChanged()
    {
        (ScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CancelScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RegisterSelectedCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private static string DescribeResult(CandidateOrchestrationResult result)
    {
        if (result.Status == CandidateOrchestrationStatus.Registered)
        {
            return "Registered.";
        }

        if (result.Status == CandidateOrchestrationStatus.DuplicateSkipped)
        {
            return "Skipped (duplicate).";
        }

        return result.Reasons.Count > 0 ? result.Reasons[0].Message : "Registration failed.";
    }
}
