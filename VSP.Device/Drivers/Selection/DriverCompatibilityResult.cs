using VSP.Device.Drivers.Settings;
using VSP.Domain.Enums;

namespace VSP.Device.Drivers.Selection;

public sealed class DriverCompatibilityResult
{
    public string DriverId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public DeviceConnectionType ConnectionType { get; init; } = DeviceConnectionType.Unknown;

    public DriverCompatibilityStatus Status { get; init; }

    public IReadOnlyList<DriverEvidenceEvaluation> RequiredEvidenceEvaluations { get; init; } =
        Array.Empty<DriverEvidenceEvaluation>();

    public IReadOnlyList<DriverEvidenceEvaluation> OptionalEvidenceEvaluations { get; init; } =
        Array.Empty<DriverEvidenceEvaluation>();

    public IReadOnlyList<DriverEvidenceEvaluation> UnsupportedEvidenceEvaluations { get; init; } =
        Array.Empty<DriverEvidenceEvaluation>();

    public IReadOnlyList<DriverSettingKey> RequiredSettings { get; init; } = Array.Empty<DriverSettingKey>();

    public IReadOnlyList<DriverSelectionReason> MatchReasons { get; init; } =
        Array.Empty<DriverSelectionReason>();

    public IReadOnlyList<DriverSelectionReason> RejectionReasons { get; init; } =
        Array.Empty<DriverSelectionReason>();
}
