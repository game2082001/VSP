using VSP.Device.Interfaces;
using VSP.Device.Services;
using VSP.Domain.Enums;
using VSP.UI.ViewModels;
using EntityCamera = VSP.Domain.Entities.Camera;
using Xunit;

namespace VSP.Tests.Camera;

public class BatchConnectionTestViewModelTests
{
    [Fact]
    public void Constructor_RunsTestsAndPopulatesResults()
    {
        var tester = new FakeCameraConnectionTester(success: true);
        var repository = new RecordingCameraRepository();
        var cameras = new[] { CreateCamera("A"), CreateCamera("B") };

        var viewModel = new BatchConnectionTestViewModel(cameras, tester, repository);

        Assert.Equal(2, viewModel.Results.Count);
        Assert.All(viewModel.Results, result => Assert.True(result.IsSuccess));
        Assert.Equal("2 succeeded.", viewModel.StatusMessage);
    }

    [Fact]
    public void Constructor_WithMixedResults_ReportsSuccessAndFailureCounts()
    {
        var tester = new FakeCameraConnectionTester(failOnCameraName: "bad");
        var repository = new RecordingCameraRepository();
        var cameras = new[] { CreateCamera("good"), CreateCamera("bad") };

        var viewModel = new BatchConnectionTestViewModel(cameras, tester, repository);

        Assert.Equal("1 succeeded, 1 failed.", viewModel.StatusMessage);
        Assert.Contains(viewModel.Results, r => r.Name == "good" && r.IsSuccess);
        Assert.Contains(viewModel.Results, r => r.Name == "bad" && !r.IsSuccess);
    }

    [Fact]
    public void Constructor_PersistsStatusOnTestedCameras()
    {
        var tester = new FakeCameraConnectionTester(failOnCameraName: "bad");
        var repository = new RecordingCameraRepository();
        var good = CreateCamera("good");
        var bad = CreateCamera("bad");

        _ = new BatchConnectionTestViewModel(new[] { good, bad }, tester, repository);

        Assert.Equal(CameraStatus.Online, good.Status);
        Assert.Equal(CameraStatus.Offline, bad.Status);
        Assert.Equal(2, repository.Updated.Count);
        Assert.Contains(repository.Updated, c => c.Name == "good" && c.Status == CameraStatus.Online);
        Assert.Contains(repository.Updated, c => c.Name == "bad" && c.Status == CameraStatus.Offline);
    }

    [Fact]
    public void CloseCommand_RaisesRequestClose()
    {
        var tester = new FakeCameraConnectionTester(success: true);
        var repository = new RecordingCameraRepository();
        var viewModel = new BatchConnectionTestViewModel(new[] { CreateCamera() }, tester, repository);

        var closeRaised = false;
        viewModel.RequestClose += () => closeRaised = true;

        viewModel.CloseCommand.Execute(null);

        Assert.True(closeRaised);
    }

    [Fact]
    public void Constructor_WithEmptyCameraList_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new BatchConnectionTestViewModel(
                Array.Empty<EntityCamera>(),
                new FakeCameraConnectionTester(success: true),
                new RecordingCameraRepository()));
    }

    [Fact]
    public void Constructor_WithNullCameras_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BatchConnectionTestViewModel(
                null!,
                new FakeCameraConnectionTester(success: true),
                new RecordingCameraRepository()));
    }

    [Fact]
    public void Constructor_WithNullConnectionTester_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BatchConnectionTestViewModel(
                new[] { CreateCamera() },
                null!,
                new RecordingCameraRepository()));
    }

    [Fact]
    public void Constructor_WithNullCameraRepository_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BatchConnectionTestViewModel(
                new[] { CreateCamera() },
                new FakeCameraConnectionTester(success: true),
                null!));
    }

    private static EntityCamera CreateCamera(string name = "Camera")
    {
        return new EntityCamera
        {
            Name = name,
            IpAddress = "192.168.1.1"
        };
    }

    private sealed class FakeCameraConnectionTester : ICameraConnectionTester
    {
        private readonly bool _defaultSuccess;
        private readonly string? _failOnCameraName;

        public FakeCameraConnectionTester(bool success = true, string? failOnCameraName = null)
        {
            _defaultSuccess = success;
            _failOnCameraName = failOnCameraName;
        }

        public CameraConnectionTestResult Test(EntityCamera camera)
        {
            var isSuccess = _failOnCameraName is not null
                ? camera.Name != _failOnCameraName
                : _defaultSuccess;

            return new CameraConnectionTestResult(camera.Id, isSuccess);
        }
    }

    private sealed class RecordingCameraRepository : ICameraRepository
    {
        public List<EntityCamera> Updated { get; } = new();

        public IEnumerable<EntityCamera> GetAll() => Updated;

        public EntityCamera? GetById(Guid id) => Updated.FirstOrDefault(c => c.Id == id);

        public void Add(EntityCamera camera) => Updated.Add(camera);

        public void Update(EntityCamera camera) => Updated.Add(camera);

        public void Delete(Guid id)
        {
        }
    }
}
