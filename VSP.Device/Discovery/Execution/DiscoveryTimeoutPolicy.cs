namespace VSP.Device.Discovery.Execution;

public sealed class DiscoveryTimeoutPolicy
{
    public static readonly DiscoveryTimeoutPolicy Default = new(TimeSpan.FromSeconds(30));

    public DiscoveryTimeoutPolicy(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be greater than zero.");
        }

        Timeout = timeout;
    }

    public TimeSpan Timeout { get; }
}
