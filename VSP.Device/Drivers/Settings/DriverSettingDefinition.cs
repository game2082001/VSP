namespace VSP.Device.Drivers.Settings;

public sealed class DriverSettingDefinition
{
    public DriverSettingDefinition(
        DriverSettingKey key,
        string displayName,
        bool isRequired = false,
        object? defaultValue = null,
        bool isSensitive = false,
        DriverSettingValueKind valueKind = DriverSettingValueKind.Text)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("DisplayName must not be empty.", nameof(displayName));
        }

        Key = key;
        DisplayName = displayName;
        IsRequired = isRequired;
        DefaultValue = defaultValue;
        IsSensitive = isSensitive;
        ValueKind = valueKind;
    }

    public DriverSettingKey Key { get; }

    public string DisplayName { get; }

    public bool IsRequired { get; }

    public object? DefaultValue { get; }

    public bool IsSensitive { get; }

    public DriverSettingValueKind ValueKind { get; }
}
