namespace VSP.Device.Discovery.NetworkScan;

public sealed class NetworkScanRequest
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    public IReadOnlyList<NetworkScanTarget> Targets { get; init; } = Array.Empty<NetworkScanTarget>();

    public int MaxConcurrency { get; init; } = 4;

    public TimeSpan Timeout { get; init; } = DefaultTimeout;

    public void Validate()
    {
        if (MaxConcurrency < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxConcurrency), "MaxConcurrency must be greater than or equal to 1.");
        }

        if (Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(Timeout), "Timeout must be greater than zero.");
        }
    }
}
