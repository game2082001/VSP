using VSP.Device.Drivers.Settings;
using VSP.UI.ViewModels;
using Xunit;

namespace VSP.Tests.Camera;

public class DriverSettingEditorViewModelTests
{
    [Fact]
    public void Constructor_ExposesDefinitionMetadata()
    {
        var definition = new DriverSettingDefinition(DriverSettingKey.HttpPort, "HTTP Port", valueKind: DriverSettingValueKind.Port);

        var viewModel = new DriverSettingEditorViewModel(definition, "80");

        Assert.Equal(DriverSettingKey.HttpPort, viewModel.Key);
        Assert.Equal("HTTP Port", viewModel.DisplayName);
        Assert.Equal("80", viewModel.Value);
        Assert.False(viewModel.IsSensitive);
    }

    [Fact]
    public void DisplayValue_WhenSensitive_IsMasked()
    {
        var definition = new DriverSettingDefinition(DriverSettingKey.Password, "Password", isSensitive: true);

        var viewModel = new DriverSettingEditorViewModel(definition, "secret");

        Assert.Equal("******", viewModel.DisplayValue);
    }

    [Fact]
    public void DisplayValue_WhenNotSensitive_IsRawValue()
    {
        var definition = new DriverSettingDefinition(DriverSettingKey.Username, "Username");

        var viewModel = new DriverSettingEditorViewModel(definition, "admin");

        Assert.Equal("admin", viewModel.DisplayValue);
    }

    [Fact]
    public void Validate_RequiredAndEmpty_IsInvalid()
    {
        var definition = new DriverSettingDefinition(DriverSettingKey.RtspUrl, "RTSP URL", isRequired: true);

        var viewModel = new DriverSettingEditorViewModel(definition, string.Empty);

        Assert.False(viewModel.IsValid);
        Assert.Equal("RTSP URL is required.", viewModel.ErrorMessage);
    }

    [Fact]
    public void Validate_NotRequiredAndEmpty_IsValid()
    {
        var definition = new DriverSettingDefinition(DriverSettingKey.Username, "Username");

        var viewModel = new DriverSettingEditorViewModel(definition, string.Empty);

        Assert.True(viewModel.IsValid);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("65535", true)]
    [InlineData("0", false)]
    [InlineData("65536", false)]
    [InlineData("not-a-number", false)]
    public void Validate_PortKind_EnforcesRange(string value, bool expectedValid)
    {
        var definition = new DriverSettingDefinition(DriverSettingKey.HttpPort, "HTTP Port", valueKind: DriverSettingValueKind.Port);
        var viewModel = new DriverSettingEditorViewModel(definition, "80");

        viewModel.Value = value;

        Assert.Equal(expectedValid, viewModel.IsValid);
    }

    [Theory]
    [InlineData("rtsp://192.168.1.10/stream", true)]
    [InlineData("not-a-url", false)]
    public void Validate_UrlKind_RequiresAbsoluteUri(string value, bool expectedValid)
    {
        var definition = new DriverSettingDefinition(DriverSettingKey.RtspUrl, "RTSP URL", valueKind: DriverSettingValueKind.Url);
        var viewModel = new DriverSettingEditorViewModel(definition, string.Empty);

        viewModel.Value = value;

        Assert.Equal(expectedValid, viewModel.IsValid);
    }

    [Fact]
    public void Validate_TextKind_AcceptsAnyNonEmptyValue()
    {
        var definition = new DriverSettingDefinition(DriverSettingKey.Username, "Username");
        var viewModel = new DriverSettingEditorViewModel(definition, string.Empty);

        viewModel.Value = "anything at all";

        Assert.True(viewModel.IsValid);
    }

    [Fact]
    public void ValueChanged_RaisesDisplayValuePropertyChanged()
    {
        var definition = new DriverSettingDefinition(DriverSettingKey.Password, "Password", isSensitive: true);
        var viewModel = new DriverSettingEditorViewModel(definition, string.Empty);
        var raisedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName);

        viewModel.Value = "abc";

        Assert.Contains(nameof(DriverSettingEditorViewModel.DisplayValue), raisedProperties);
    }
}
