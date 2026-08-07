using VSP.Core.Configuration;
using VSP.Core.Logging;
using VSP.Infrastructure.Settings;
using VSP.Tests.Logging;
using Xunit;

namespace VSP.Tests.Infrastructure;

[Collection("AppLog")]
public class AppSettingsProviderTests : IDisposable
{
    private readonly string _configDirectory =
        Path.Combine(Path.GetTempPath(), $"vsp-app-settings-provider-test-{Guid.NewGuid():N}");

    [Fact]
    public void Load_NoFile_ReturnsAllDefaults()
    {
        var provider = new AppSettingsProvider(_configDirectory);

        var settings = provider.Load();

        Assert.Equal(RecordingRootDefaults.Compute(_configDirectory), settings.RecordingPath);
        Assert.Equal(AppSettingsLimits.DefaultRetentionDays, settings.RetentionDays);
        Assert.Equal(AppLanguage.English, settings.Language);
        Assert.Equal(AppTheme.System, settings.Theme);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsAllFields()
    {
        var provider = new AppSettingsProvider(_configDirectory);
        var settings = new AppSettings
        {
            RecordingPath = Path.Combine(_configDirectory, "CustomRecordings"),
            RetentionDays = 90,
            Language = AppLanguage.TraditionalChinese,
            Theme = AppTheme.Light
        };

        provider.Save(settings);
        var loaded = provider.Load();

        Assert.Equal(settings.RecordingPath, loaded.RecordingPath);
        Assert.Equal(settings.RetentionDays, loaded.RetentionDays);
        Assert.Equal(settings.Language, loaded.Language);
        Assert.Equal(settings.Theme, loaded.Theme);
    }

    [Fact]
    public void Load_LegacyFileWithOnlyRecordingRoot_OtherFieldsDefault()
    {
        Directory.CreateDirectory(_configDirectory);
        var legacyRoot = Path.Combine(_configDirectory, "LegacyRecordings");
        File.WriteAllText(
            Path.Combine(_configDirectory, "recording-settings.json"),
            $"{{ \"RecordingRoot\": {System.Text.Json.JsonSerializer.Serialize(legacyRoot)} }}");

        var settings = new AppSettingsProvider(_configDirectory).Load();

        Assert.Equal(legacyRoot, settings.RecordingPath);
        Assert.Equal(AppSettingsLimits.DefaultRetentionDays, settings.RetentionDays);
        Assert.Equal(AppLanguage.English, settings.Language);
        Assert.Equal(AppTheme.System, settings.Theme);
    }

    [Fact]
    public void Load_BlankRecordingRoot_FallsBackToDefault()
    {
        Directory.CreateDirectory(_configDirectory);
        File.WriteAllText(
            Path.Combine(_configDirectory, "recording-settings.json"),
            "{ \"RecordingRoot\": \"   \" }");

        var settings = new AppSettingsProvider(_configDirectory).Load();

        Assert.Equal(RecordingRootDefaults.Compute(_configDirectory), settings.RecordingPath);
    }

    [Fact]
    public void Load_OutOfRangeRetentionDays_LogsWarningAndDefaults()
    {
        Directory.CreateDirectory(_configDirectory);
        File.WriteAllText(
            Path.Combine(_configDirectory, "recording-settings.json"),
            "{ \"RetentionDays\": -5 }");

        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);

        var settings = new AppSettingsProvider(_configDirectory).Load();

        Assert.Equal(AppSettingsLimits.DefaultRetentionDays, settings.RetentionDays);
        Assert.Contains(recorder.Calls, c => c.Level == LogLevel.Warning);
    }

    [Fact]
    public void Load_UnrecognizedLanguageCode_LogsWarningAndDefaultsToEnglish()
    {
        Directory.CreateDirectory(_configDirectory);
        File.WriteAllText(
            Path.Combine(_configDirectory, "recording-settings.json"),
            "{ \"Language\": \"fr-FR\" }");

        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);

        var settings = new AppSettingsProvider(_configDirectory).Load();

        Assert.Equal(AppLanguage.English, settings.Language);
        Assert.Contains(recorder.Calls, c => c.Level == LogLevel.Warning);
    }

    [Fact]
    public void Load_UnrecognizedThemeValue_LogsWarningAndDefaultsToSystem()
    {
        Directory.CreateDirectory(_configDirectory);
        File.WriteAllText(
            Path.Combine(_configDirectory, "recording-settings.json"),
            "{ \"Theme\": \"Purple\" }");

        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);

        var settings = new AppSettingsProvider(_configDirectory).Load();

        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.Contains(recorder.Calls, c => c.Level == LogLevel.Warning);
    }

    [Fact]
    public void Save_ChangingOneField_PreservesPreviouslySavedFields()
    {
        var provider = new AppSettingsProvider(_configDirectory);
        provider.Save(new AppSettings
        {
            RecordingPath = Path.Combine(_configDirectory, "Recordings"),
            RetentionDays = 60,
            Language = AppLanguage.TraditionalChinese,
            Theme = AppTheme.Dark
        });

        var current = provider.Load();
        provider.Save(new AppSettings
        {
            RecordingPath = current.RecordingPath,
            RetentionDays = current.RetentionDays,
            Language = current.Language,
            Theme = AppTheme.Light
        });
        var loaded = provider.Load();

        Assert.Equal(60, loaded.RetentionDays);
        Assert.Equal(AppLanguage.TraditionalChinese, loaded.Language);
        Assert.Equal(AppTheme.Light, loaded.Theme);
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
