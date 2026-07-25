using System.Collections.ObjectModel;
using System.Windows.Input;
using VSP.Core.Commands;
using VSP.Core.MVVM;
using VSP.Device.Interfaces;
using VSP.Device.Services;
using VSP.Domain.Entities;
using VSP.Domain.Enums;

namespace VSP.UI.ViewModels;

public class BatchConnectionTestViewModel : ObservableObject
{
    private string _statusMessage;

    public ObservableCollection<BatchConnectionTestItemViewModel> Results { get; } = new();

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand CloseCommand { get; }

    public event Action? RequestClose;

    public BatchConnectionTestViewModel(
        IReadOnlyList<Camera> cameras,
        ICameraConnectionTester connectionTester,
        ICameraRepository cameraRepository)
    {
        ArgumentNullException.ThrowIfNull(cameras);
        ArgumentNullException.ThrowIfNull(connectionTester);
        ArgumentNullException.ThrowIfNull(cameraRepository);

        if (cameras.Count == 0)
        {
            throw new ArgumentException("At least one camera must be provided.", nameof(cameras));
        }

        CloseCommand = new RelayCommand(() => RequestClose?.Invoke());

        _statusMessage = "Testing...";
        RunTests(cameras, connectionTester, cameraRepository);
    }

    private void RunTests(
        IReadOnlyList<Camera> cameras,
        ICameraConnectionTester connectionTester,
        ICameraRepository cameraRepository)
    {
        var successCount = 0;
        var failureCount = 0;

        foreach (var camera in cameras)
        {
            var result = connectionTester.Test(camera);
            Results.Add(new BatchConnectionTestItemViewModel(camera.Name, camera.IpAddress, result.IsSuccess));

            camera.Status = result.IsSuccess ? CameraStatus.Online : CameraStatus.Offline;
            cameraRepository.Update(camera);

            if (result.IsSuccess)
            {
                successCount++;
            }
            else
            {
                failureCount++;
            }
        }

        StatusMessage = failureCount == 0
            ? $"{successCount} succeeded."
            : $"{successCount} succeeded, {failureCount} failed.";
    }
}
