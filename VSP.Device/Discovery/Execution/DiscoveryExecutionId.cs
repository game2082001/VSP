namespace VSP.Device.Discovery.Execution;

public readonly record struct DiscoveryExecutionId
{
    private DiscoveryExecutionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Discovery execution id must not be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static DiscoveryExecutionId New()
    {
        return new DiscoveryExecutionId(Guid.NewGuid());
    }

    public static DiscoveryExecutionId FromGuid(Guid value)
    {
        return new DiscoveryExecutionId(value);
    }

    public override string ToString()
    {
        return Value.ToString();
    }
}
