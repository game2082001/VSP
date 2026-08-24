using VSP.Core.MVVM;
using VSP.Device.Drivers.Settings;

namespace VSP.UI.ViewModels;

/// <summary>
/// A single editable field, driven entirely by one <see cref="DriverSettingDefinition"/> —
/// display, masking, and validation all come from the definition's data
/// (<see cref="DriverSettingDefinition.IsSensitive"/>, <see cref="DriverSettingDefinition.ValueKind"/>,
/// <see cref="DriverSettingDefinition.IsRequired"/>), not from any per-driver or per-key
/// conditional in the consuming View or ViewModel.
/// </summary>
public class DriverSettingEditorViewModel : ObservableObject
{
    public DriverSettingDefinition Definition { get; }

    public DriverSettingKey Key => Definition.Key;

    public string DisplayName => Definition.DisplayName;

    public bool IsRequired => Definition.IsRequired;

    public bool IsSensitive => Definition.IsSensitive;

    public DriverSettingValueKind ValueKind => Definition.ValueKind;

    private string _value;
    public string Value
    {
        get => _value;
        set
        {
            // Marked unconditionally, before the equality check inside SetProperty, so that
            // explicitly re-entering the value the field already holds (e.g. confirming "554"
            // when it already reads "554") still counts as an explicit edit.
            WasExplicitlyEdited = true;

            if (SetProperty(ref _value, value))
            {
                Validate();
                OnPropertyChanged(nameof(DisplayValue));
            }
        }
    }

    public string DisplayValue => IsSensitive ? MaskValue(Value) : Value;

    /// <summary>
    /// True once this instance's <see cref="Value"/> setter has been invoked via user-driven
    /// binding. Construction and <c>RebuildDriverSettings</c> reseeding assign the backing field
    /// directly and never set this. Generic on this shared view-model; individual consumers
    /// decide whether/which key's flag matters to them.
    /// </summary>
    public bool WasExplicitlyEdited { get; private set; }

    private bool _isValid = true;
    public bool IsValid
    {
        get => _isValid;
        private set => SetProperty(ref _isValid, value);
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public DriverSettingEditorViewModel(DriverSettingDefinition definition, string initialValue)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _value = initialValue ?? string.Empty;
        Validate();
    }

    public void PreserveExplicitEdit()
    {
        WasExplicitlyEdited = true;
    }

    private void Validate()
    {
        if (IsRequired && string.IsNullOrWhiteSpace(Value))
        {
            SetInvalid($"{DisplayName} is required.");
            return;
        }

        if (string.IsNullOrWhiteSpace(Value))
        {
            SetValid();
            return;
        }

        switch (ValueKind)
        {
            case DriverSettingValueKind.Port:
                if (!int.TryParse(Value, out var port) || port is < 1 or > 65535)
                {
                    SetInvalid($"{DisplayName} must be between 1 and 65535.");
                    return;
                }

                break;

            case DriverSettingValueKind.Url:
                if (!Uri.TryCreate(Value, UriKind.Absolute, out _))
                {
                    SetInvalid($"{DisplayName} must be a valid URL.");
                    return;
                }

                break;
        }

        SetValid();
    }

    private void SetValid()
    {
        IsValid = true;
        ErrorMessage = string.Empty;
    }

    private void SetInvalid(string message)
    {
        IsValid = false;
        ErrorMessage = message;
    }

    private static string MaskValue(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : new string('*', value.Length);
    }
}
