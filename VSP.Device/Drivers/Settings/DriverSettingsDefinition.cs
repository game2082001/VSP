namespace VSP.Device.Drivers.Settings;

public sealed class DriverSettingsDefinition
{
    private readonly IReadOnlyList<DriverSettingDefinition> _settings;

    public DriverSettingsDefinition(IEnumerable<DriverSettingDefinition> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var copiedSettings = settings.ToList();
        if (copiedSettings.Any(static setting => setting is null))
        {
            throw new InvalidOperationException("Driver settings must not contain null entries.");
        }

        _settings = copiedSettings.AsReadOnly();
    }

    public IReadOnlyList<DriverSettingDefinition> Settings => _settings;
}
