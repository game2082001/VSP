namespace VSP.Device.Discovery.Execution;

public sealed class DiscoveryTimeoutException : Exception
{
    public DiscoveryTimeoutException(TimeSpan timeout)
        : base($"Discovery execution exceeded its operation timeout of {timeout}.")
    {
        Timeout = timeout;
    }

    public TimeSpan Timeout { get; }
}
