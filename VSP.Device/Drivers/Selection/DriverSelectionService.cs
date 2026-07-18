using VSP.Device.Drivers.Capabilities;

namespace VSP.Device.Drivers.Selection;

public sealed class DriverSelectionService
{
    public DriverSelectionResult Evaluate(DriverSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Evidence);
        ArgumentNullException.ThrowIfNull(request.DriverDescriptors);

        var results = request.DriverDescriptors
            .Select(descriptor => EvaluateDescriptor(descriptor, request.Evidence))
            .ToArray();

        var reasons = results.Any(result => result.Status == DriverCompatibilityStatus.Compatible)
            ? Array.Empty<DriverSelectionReason>()
            : new[]
            {
                new DriverSelectionReason(
                    "NoCompatibleDriver",
                    "No compatible driver was found for the supplied evidence.")
            };

        return new DriverSelectionResult
        {
            CompatibilityResults = results,
            Reasons = reasons
        };
    }

    private static DriverCompatibilityResult EvaluateDescriptor(
        DriverDescriptor descriptor,
        DriverEvidenceCollection evidence)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var capability = descriptor.CompatibilityCapability ?? DriverCompatibilityCapability.Empty;
        var requiredEvaluations = capability.RequiredEvidence
            .Select(requirement => EvaluateRequiredEvidence(requirement, evidence))
            .ToArray();
        var optionalEvaluations = capability.OptionalEvidence
            .Select(requirement => EvaluateOptionalEvidence(requirement, evidence))
            .ToArray();
        var unsupportedEvaluations = capability.UnsupportedEvidence
            .Select(requirement => EvaluateUnsupportedEvidence(requirement, evidence))
            .ToArray();
        var rejectionReasons = BuildRejectionReasons(requiredEvaluations, unsupportedEvaluations);
        var matchReasons = rejectionReasons.Count == 0
            ? BuildMatchReasons(descriptor.DriverId, requiredEvaluations, optionalEvaluations)
            : Array.Empty<DriverSelectionReason>();

        return new DriverCompatibilityResult
        {
            DriverId = descriptor.DriverId,
            DisplayName = descriptor.DisplayName,
            ConnectionType = descriptor.ConnectionType,
            Status = ResolveStatus(requiredEvaluations, unsupportedEvaluations),
            RequiredEvidenceEvaluations = requiredEvaluations,
            OptionalEvidenceEvaluations = optionalEvaluations,
            UnsupportedEvidenceEvaluations = unsupportedEvaluations,
            RequiredSettings = capability.RequiredSettings,
            MatchReasons = matchReasons,
            RejectionReasons = rejectionReasons
        };
    }

    private static DriverEvidenceEvaluation EvaluateRequiredEvidence(
        DriverCompatibilityEvidenceRequirement requirement,
        DriverEvidenceCollection evidence)
    {
        var satisfied = evidence.Contains(requirement);
        return new DriverEvidenceEvaluation(
            requirement.Kind,
            satisfied ? DriverEvidenceEvaluationStatus.Satisfied : DriverEvidenceEvaluationStatus.Missing,
            satisfied
                ? CreateReason("RequiredEvidenceSatisfied", $"Required evidence '{requirement.Kind}' is present.")
                : CreateReason("RequiredEvidenceMissing", $"Required evidence '{requirement.Kind}' is missing."),
            requirement.Qualifier);
    }

    private static DriverEvidenceEvaluation EvaluateOptionalEvidence(
        DriverCompatibilityEvidenceRequirement requirement,
        DriverEvidenceCollection evidence)
    {
        var satisfied = evidence.Contains(requirement);
        return new DriverEvidenceEvaluation(
            requirement.Kind,
            satisfied ? DriverEvidenceEvaluationStatus.Satisfied : DriverEvidenceEvaluationStatus.Missing,
            satisfied
                ? CreateReason("OptionalEvidenceSatisfied", $"Optional evidence '{requirement.Kind}' is present.")
                : CreateReason("OptionalEvidenceMissing", $"Optional evidence '{requirement.Kind}' is missing."),
            requirement.Qualifier);
    }

    private static DriverEvidenceEvaluation EvaluateUnsupportedEvidence(
        DriverCompatibilityEvidenceRequirement requirement,
        DriverEvidenceCollection evidence)
    {
        var conflict = evidence.Contains(requirement);
        return new DriverEvidenceEvaluation(
            requirement.Kind,
            conflict ? DriverEvidenceEvaluationStatus.Conflict : DriverEvidenceEvaluationStatus.NotApplicable,
            conflict
                ? CreateReason("UnsupportedEvidenceConflict", $"Unsupported evidence '{requirement.Kind}' is present.")
                : CreateReason("UnsupportedEvidenceNotApplicable", $"Unsupported evidence '{requirement.Kind}' is not present."),
            requirement.Qualifier);
    }

    private static DriverCompatibilityStatus ResolveStatus(
        IReadOnlyCollection<DriverEvidenceEvaluation> requiredEvaluations,
        IReadOnlyCollection<DriverEvidenceEvaluation> unsupportedEvaluations)
    {
        if (unsupportedEvaluations.Any(evaluation => evaluation.Status == DriverEvidenceEvaluationStatus.Conflict))
        {
            return DriverCompatibilityStatus.Rejected;
        }

        if (requiredEvaluations.Any(evaluation => evaluation.Status == DriverEvidenceEvaluationStatus.Missing))
        {
            return DriverCompatibilityStatus.InsufficientEvidence;
        }

        return DriverCompatibilityStatus.Compatible;
    }

    private static IReadOnlyList<DriverSelectionReason> BuildRejectionReasons(
        IEnumerable<DriverEvidenceEvaluation> requiredEvaluations,
        IEnumerable<DriverEvidenceEvaluation> unsupportedEvaluations)
    {
        return requiredEvaluations
            .Where(evaluation => evaluation.Status == DriverEvidenceEvaluationStatus.Missing)
            .Concat(unsupportedEvaluations.Where(evaluation => evaluation.Status == DriverEvidenceEvaluationStatus.Conflict))
            .Select(evaluation => evaluation.Reason)
            .ToArray();
    }

    private static IReadOnlyList<DriverSelectionReason> BuildMatchReasons(
        string driverId,
        IReadOnlyCollection<DriverEvidenceEvaluation> requiredEvaluations,
        IReadOnlyCollection<DriverEvidenceEvaluation> optionalEvaluations)
    {
        if (requiredEvaluations.Count == 0)
        {
            return
            [
                new DriverSelectionReason(
                    "CompatibleWithoutRequiredEvidence",
                    $"Driver '{driverId}' has no required evidence requirements and is compatible.")
            ];
        }

        var satisfiedRequiredCount = requiredEvaluations.Count(
            evaluation => evaluation.Status == DriverEvidenceEvaluationStatus.Satisfied);
        var satisfiedOptionalCount = optionalEvaluations.Count(
            evaluation => evaluation.Status == DriverEvidenceEvaluationStatus.Satisfied);

        return
        [
            new DriverSelectionReason(
                "Compatible",
                $"Driver '{driverId}' satisfied {satisfiedRequiredCount} required evidence requirement(s) and {satisfiedOptionalCount} optional evidence item(s).")
        ];
    }

    private static DriverSelectionReason CreateReason(string code, string message)
    {
        return new DriverSelectionReason(code, message);
    }
}
