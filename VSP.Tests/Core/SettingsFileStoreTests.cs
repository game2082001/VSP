using VSP.Core.Configuration;
using VSP.Core.Logging;
using VSP.Tests.Logging;
using Xunit;

namespace VSP.Tests.Core;

[Collection("AppLog")]
public class SettingsFileStoreTests : IDisposable
{
    private readonly string _configDirectory =
        Path.Combine(Path.GetTempPath(), $"vsp-settings-file-store-test-{Guid.NewGuid():N}");

    [Fact]
    public void Load_NoFile_ReturnsAllNullContents()
    {
        var store = new SettingsFileStore(_configDirectory);

        var contents = store.Load();

        Assert.Null(contents.RecordingRoot);
        Assert.Null(contents.RetentionDays);
        Assert.Null(contents.Language);
        Assert.Null(contents.Theme);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsAllFields()
    {
        var store = new SettingsFileStore(_configDirectory);
        var contents = new SettingsFileContents
        {
            RecordingRoot = Path.Combine(_configDirectory, "CustomRecordings"),
            RetentionDays = 45,
            Language = "zh-TW",
            Theme = "Light"
        };

        store.Save(contents);
        var loaded = store.Load();

        Assert.Equal(contents.RecordingRoot, loaded.RecordingRoot);
        Assert.Equal(contents.RetentionDays, loaded.RetentionDays);
        Assert.Equal(contents.Language, loaded.Language);
        Assert.Equal(contents.Theme, loaded.Theme);
    }

    [Fact]
    public void Load_FileWithOnlyLegacyRecordingRoot_OtherFieldsAreNull()
    {
        Directory.CreateDirectory(_configDirectory);
        File.WriteAllText(
            Path.Combine(_configDirectory, "recording-settings.json"),
            "{ \"RecordingRoot\": \"C:\\\\Legacy\\\\Recordings\" }");

        var loaded = new SettingsFileStore(_configDirectory).Load();

        Assert.Equal("C:\\Legacy\\Recordings", loaded.RecordingRoot);
        Assert.Null(loaded.RetentionDays);
        Assert.Null(loaded.Language);
        Assert.Null(loaded.Theme);
    }

    [Fact]
    public void Load_WithUnknownProperties_DoesNotThrowAndIgnoresThem()
    {
        Directory.CreateDirectory(_configDirectory);
        File.WriteAllText(
            Path.Combine(_configDirectory, "recording-settings.json"),
            "{ \"RecordingRoot\": \"C:\\\\Recordings\", \"SomeFutureField\": 123 }");

        var loaded = new SettingsFileStore(_configDirectory).Load();

        Assert.Equal("C:\\Recordings", loaded.RecordingRoot);
    }

    [Fact]
    public void Load_WithMalformedJson_LogsWarningAndReturnsDefaults()
    {
        Directory.CreateDirectory(_configDirectory);
        File.WriteAllText(Path.Combine(_configDirectory, "recording-settings.json"), "{ not valid json");

        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);

        var loaded = new SettingsFileStore(_configDirectory).Load();

        Assert.Null(loaded.RecordingRoot);
        Assert.Null(loaded.RetentionDays);
        Assert.Null(loaded.Language);
        Assert.Null(loaded.Theme);
        Assert.Single(recorder.Calls);
        Assert.Equal(LogLevel.Warning, recorder.Calls[0].Level);
        Assert.NotNull(recorder.Calls[0].Exception);
    }

    [Fact]
    public void Save_LeavesNoTempFileBehind()
    {
        var store = new SettingsFileStore(_configDirectory);

        store.Save(new SettingsFileContents { RecordingRoot = "C:\\Recordings" });

        var files = Directory.GetFiles(_configDirectory);
        Assert.Single(files);
        Assert.Equal("recording-settings.json", Path.GetFileName(files[0]));
    }

    [Fact]
    public void Save_OverwritesExistingFile()
    {
        var store = new SettingsFileStore(_configDirectory);
        store.Save(new SettingsFileContents { RetentionDays = 10 });

        store.Save(new SettingsFileContents { RetentionDays = 60 });
        var loaded = store.Load();

        Assert.Equal(60, loaded.RetentionDays);
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
