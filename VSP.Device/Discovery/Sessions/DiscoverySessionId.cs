namespace VSP.Device.Discovery.Sessions;

public readonly record struct DiscoverySessionId
{
    private DiscoverySessionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Discovery session id must not be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static DiscoverySessionId New()
    {
        return new DiscoverySessionId(Guid.NewGuid());
    }

    public static DiscoverySessionId FromGuid(Guid value)
    {
        return new DiscoverySessionId(value);
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
