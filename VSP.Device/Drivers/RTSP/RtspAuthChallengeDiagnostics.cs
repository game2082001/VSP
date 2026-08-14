namespace VSP.Device.Drivers.RTSP;

/// <summary>
/// Task-AI00B Phase 10: builds the one sanitized diagnostic summary line for the real
/// WWW-Authenticate challenge(s) a camera returns on TestConnection's first 401, so a real
/// device's actual challenge shape (algorithm, qop, stale, multiple challenges) can be observed
/// without ever logging a secret or protocol value that could leak credentials or aid replay
/// (realm/nonce/opaque values, the Authorization header, or any RTSP URI). Read-only: this class
/// only formats a string from an already-parsed <see cref="RtspAuthChallenge"/> list and never
/// influences which challenge RtspCameraDriver selects or how it builds Authorization.
/// </summary>
public static class RtspAuthChallengeDiagnostics
{
    public static string BuildSummary(
        IReadOnlyList<RtspAuthChallenge> challenges,
        int selectedChallengeIndex,
        int? secondResponseStatus)
    {
        var selected = selectedChallengeIndex >= 0 && selectedChallengeIndex < challenges.Count
            ? challenges[selectedChallengeIndex]
            : null;

        var qop = selected?.Qop;
        var qopTokens = string.IsNullOrWhiteSpace(qop)
            ? []
            : qop.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var algorithmPresent = !string.IsNullOrEmpty(selected?.Algorithm);
        var stalePresent = !string.IsNullOrEmpty(selected?.Stale);

        return "RTSP TestConnection auth challenge: " +
            $"authChallengeCount={challenges.Count}, " +
            $"challengeSchemes={string.Join(",", challenges.Select(static c => c.Scheme))}, " +
            $"selectedChallengeIndex={selectedChallengeIndex}, " +
            $"selectedScheme={selected?.Scheme ?? string.Empty}, " +
            $"algorithmPresent={algorithmPresent}, " +
            $"algorithmValue={(algorithmPresent ? selected!.Algorithm : string.Empty)}, " +
            $"qopPresent={!string.IsNullOrEmpty(qop)}, " +
            $"qopHasAuth={qopTokens.Any(static t => t.Equals("auth", StringComparison.OrdinalIgnoreCase))}, " +
            $"qopHasAuthInt={qopTokens.Any(static t => t.Equals("auth-int", StringComparison.OrdinalIgnoreCase))}, " +
            $"realmPresent={!string.IsNullOrEmpty(selected?.Realm)}, " +
            $"noncePresent={!string.IsNullOrEmpty(selected?.Nonce)}, " +
            $"opaquePresent={!string.IsNullOrEmpty(selected?.Opaque)}, " +
            $"stalePresent={stalePresent}, " +
            $"staleValue={(stalePresent ? selected!.Stale : string.Empty)}, " +
            $"secondResponseStatus={(secondResponseStatus?.ToString() ?? string.Empty)}";
    }
}
