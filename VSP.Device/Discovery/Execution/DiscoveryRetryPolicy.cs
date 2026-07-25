namespace VSP.Device.Discovery.Execution;

public sealed class DiscoveryRetryPolicy
{
    public static readonly DiscoveryRetryPolicy Default = new(maxAttempts: 3, delay: TimeSpan.FromSeconds(1));

    public DiscoveryRetryPolicy(int maxAttempts, TimeSpan delay)
    {
        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Max attempts must be at least 1.");
        }

        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), "Delay must not be negative.");
        }

        MaxAttempts = maxAttempts;
        Delay = delay;
    }

    public int MaxAttempts { get; }

    public TimeSpan Delay { get; }
}
