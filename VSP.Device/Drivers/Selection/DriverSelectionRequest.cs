namespace VSP.Device.Drivers.Selection;

public sealed class DriverSelectionRequest
{
    public DriverEvidenceCollection Evidence { get; init; } = new();

    public IReadOnlyList<DriverDescriptor> DriverDescriptors { get; init; } = Array.Empty<DriverDescriptor>();
}
