using VSP.Infrastructure.Settings;
using VSP.UI.Validation;
using Xunit;

namespace VSP.Tests.UI;

public class SettingsValidatorTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"vsp-settings-validator-test-{Guid.NewGuid():N}");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidRecordingPath_BlankOrNull_ReturnsFalse(string? path)
    {
        Assert.False(SettingsValidator.IsValidRecordingPath(path));
    }

    [Fact]
    public void IsValidRecordingPath_SyntacticallyValidPath_ReturnsTrue()
    {
        Assert.True(SettingsValidator.IsValidRecordingPath(@"C:\Recordings"));
    }

    [Fact]
    public void IsValidRecordingPath_ContainsInvalidCharacter_ReturnsFalse()
    {
        Assert.False(SettingsValidator.IsValidRecordingPath("C:\\Some\0Path"));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    [InlineData(30, true)]
    [InlineData(AppSettingsLimits.MaxRetentionDays, true)]
    [InlineData(AppSettingsLimits.MaxRetentionDays + 1, false)]
    public void IsValidRetentionDays_BoundaryValues(int days, bool expected)
    {
        Assert.Equal(expected, SettingsValidator.IsValidRetentionDays(days));
    }

    [Fact]
    public void HasWriteAccess_WritableDirectory_ReturnsTrueAndLeavesNoFileBehind()
    {
        Directory.CreateDirectory(_tempDirectory);

        var result = SettingsValidator.HasWriteAccess(_tempDirectory);

        Assert.True(result);
        Assert.Empty(Directory.GetFiles(_tempDirectory));
    }

    [Fact]
    public void HasWriteAccess_NonExistentDirectory_ReturnsFalseWithoutThrowing()
    {
        var missingDirectory = Path.Combine(_tempDirectory, "does-not-exist");

        var result = SettingsValidator.HasWriteAccess(missingDirectory);

        Assert.False(result);
    }

    // HasWriteAccess's cleanup-failure -> AppLog.Warning path (finally block's own catch) is not
    // separately unit-tested here: forcing the internal, uniquely-named probe file's own delete
    // to fail deterministically (without adding a test-only injection point beyond the approved
    // API) would require a real concurrent file lock racing against a GUID-named file this test
    // can't predict in advance. The pattern itself (catch -> AppLog.Warning -> swallow) is
    // exercised elsewhere (SettingsFileStoreTests, AppSettingsProviderTests); this specific
    // trigger is covered by code review and the Epic-016 manual validation pass instead.

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
