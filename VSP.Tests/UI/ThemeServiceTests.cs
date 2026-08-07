using VSP.Core.Logging;
using VSP.Infrastructure.Settings;
using VSP.Tests.Logging;
using VSP.UI.Services;
using Xunit;

namespace VSP.Tests.UI;

/// <summary>
/// Covers only the System-resolution logic via the injected registry-read seam
/// (<see cref="ThemeService.ResolveSystemTheme(Func{int?})"/>). The live
/// <c>Application.Current.Resources.MergedDictionaries</c> swap needs a running WPF
/// <c>Application</c> and is validated manually instead (Epic-016 Task Plan), matching the tier
/// Epic-014 used for its exception dialogs.
/// </summary>
[Collection("AppLog")]
public class ThemeServiceTests
{
    [Fact]
    public void ResolveSystemTheme_RegistryValueOne_ReturnsLight()
    {
        var result = ThemeService.ResolveSystemTheme(() => 1);

        Assert.Equal(AppTheme.Light, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void ResolveSystemTheme_RegistryValueNotOne_ReturnsDark(int value)
    {
        var result = ThemeService.ResolveSystemTheme(() => value);

        Assert.Equal(AppTheme.Dark, result);
    }

    [Fact]
    public void ResolveSystemTheme_RegistryValueMissing_ReturnsDark()
    {
        var result = ThemeService.ResolveSystemTheme(() => null);

        Assert.Equal(AppTheme.Dark, result);
    }

    [Fact]
    public void ResolveSystemTheme_RegistryReadThrows_ReturnsDarkAndLogsWarning()
    {
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);

        var result = ThemeService.ResolveSystemTheme(() => throw new InvalidOperationException("registry unavailable"));

        Assert.Equal(AppTheme.Dark, result);
        Assert.Contains(recorder.Calls, c => c.Level == LogLevel.Warning);
    }
}
