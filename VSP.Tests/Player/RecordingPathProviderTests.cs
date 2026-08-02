using System.Text.Json;
using VSP.Player.Recording;
using Xunit;

namespace VSP.Tests.Player;

public class RecordingPathProviderTests : IDisposable
{
    private readonly string _configDirectory =
        Path.Combine(Path.GetTempPath(), $"vsp-recording-path-test-{Guid.NewGuid():N}");

    [Fact]
    public void GetRecordingRoot_NoConfigFile_ReturnsDefaultAndCreatesDirectory()
    {
        var root = RecordingPathProvider.GetRecordingRoot(_configDirectory);

        Assert.Equal(Path.Combine(_configDirectory, "Recordings"), root);
        Assert.True(Directory.Exists(root));
    }

    [Fact]
    public void GetRecordingRoot_WithConfiguredRoot_UsesConfiguredValueAndCreatesDirectory()
    {
        Directory.CreateDirectory(_configDirectory);
        var configuredRoot = Path.Combine(_configDirectory, "CustomRecordings");
        File.WriteAllText(
            Path.Combine(_configDirectory, "recording-settings.json"),
            $"{{ \"RecordingRoot\": {JsonSerializer.Serialize(configuredRoot)} }}");

        var root = RecordingPathProvider.GetRecordingRoot(_configDirectory);

        Assert.Equal(configuredRoot, root);
        Assert.True(Directory.Exists(root));
    }

    [Fact]
    public void GetRecordingRoot_WithMalformedConfigFile_FallsBackToDefault()
    {
        Directory.CreateDirectory(_configDirectory);
        File.WriteAllText(Path.Combine(_configDirectory, "recording-settings.json"), "{ not valid json");

        var root = RecordingPathProvider.GetRecordingRoot(_configDirectory);

        Assert.Equal(Path.Combine(_configDirectory, "Recordings"), root);
        Assert.True(Directory.Exists(root));
    }

    [Fact]
    public void GetRecordingRoot_WithBlankConfiguredRoot_FallsBackToDefault()
    {
        Directory.CreateDirectory(_configDirectory);
        File.WriteAllText(
            Path.Combine(_configDirectory, "recording-settings.json"),
            "{ \"RecordingRoot\": \"   \" }");

        var root = RecordingPathProvider.GetRecordingRoot(_configDirectory);

        Assert.Equal(Path.Combine(_configDirectory, "Recordings"), root);
    }

    [Fact]
    public void GetCameraRecordingDirectory_CreatesSubfolderNamedByCameraId()
    {
        var cameraId = Guid.NewGuid();

        var directory = RecordingPathProvider.GetCameraRecordingDirectory(_configDirectory, cameraId);

        Assert.Equal(Path.Combine(_configDirectory, "Recordings", cameraId.ToString("N")), directory);
        Assert.True(Directory.Exists(directory));
    }

    [Fact]
    public void GetCameraRecordingDirectory_DifferentCameras_ResolveToDifferentDirectories()
    {
        var directoryA = RecordingPathProvider.GetCameraRecordingDirectory(_configDirectory, Guid.NewGuid());
        var directoryB = RecordingPathProvider.GetCameraRecordingDirectory(_configDirectory, Guid.NewGuid());

        Assert.NotEqual(directoryA, directoryB);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_configDirectory))
            {
                Directory.Delete(_configDirectory, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
