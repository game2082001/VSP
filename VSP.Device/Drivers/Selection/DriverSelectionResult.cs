namespace VSP.Device.Drivers.Selection;

public sealed class DriverSelectionResult
{
    public IReadOnlyList<DriverCompatibilityResult> CompatibilityResults { get; init; } =
        Array.Empty<DriverCompatibilityResult>();

    public IReadOnlyList<DriverSelectionReason> Reasons { get; init; } =
        Array.Empty<DriverSelectionReason>();
}
