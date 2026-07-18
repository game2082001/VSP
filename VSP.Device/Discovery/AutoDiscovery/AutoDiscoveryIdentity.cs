namespace VSP.Device.Discovery.AutoDiscovery;

public sealed class AutoDiscoveryIdentity : IEquatable<AutoDiscoveryIdentity>
{
    private AutoDiscoveryIdentity(string kind, string value)
    {
        Kind = kind;
        Value = value;
        CandidateKey = $"{kind}:{value}";
    }

    public string Kind { get; }

    public string Value { get; }

    public string CandidateKey { get; }

    public static AutoDiscoveryIdentity ForHost(string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        return new AutoDiscoveryIdentity("host", Normalize(host));
    }

    public static AutoDiscoveryIdentity ForEndpoint(string endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        return new AutoDiscoveryIdentity("endpoint", Normalize(endpoint));
    }

    public static AutoDiscoveryIdentity ForUnknown(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return new AutoDiscoveryIdentity("unknown", Normalize(value));
    }

    public bool Equals(AutoDiscoveryIdentity? other)
    {
        return other is not null
            && StringComparer.Ordinal.Equals(CandidateKey, other.CandidateKey);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as AutoDiscoveryIdentity);
    }

    public override int GetHashCode()
    {
        return StringComparer.Ordinal.GetHashCode(CandidateKey);
    }

    public override string ToString()
    {
        return CandidateKey;
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}
