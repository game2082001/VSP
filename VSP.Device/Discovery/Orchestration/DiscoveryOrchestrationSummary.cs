namespace VSP.Device.Discovery.Orchestration;

public sealed class DiscoveryOrchestrationSummary
{
    private int _totalCandidates;
    private int _registered;
    private int _approved;
    private int _awaitingApproval;
    private int _duplicateSkipped;
    private int _failed;
    private int _rejected;

    public int TotalCandidates
    {
        get => _totalCandidates;
        init => _totalCandidates = ValidateCounter(value, nameof(TotalCandidates));
    }

    public int Registered
    {
        get => _registered;
        init => _registered = ValidateCounter(value, nameof(Registered));
    }

    public int Approved
    {
        get => _approved;
        init => _approved = ValidateCounter(value, nameof(Approved));
    }

    public int AwaitingApproval
    {
        get => _awaitingApproval;
        init => _awaitingApproval = ValidateCounter(value, nameof(AwaitingApproval));
    }

    public int DuplicateSkipped
    {
        get => _duplicateSkipped;
        init => _duplicateSkipped = ValidateCounter(value, nameof(DuplicateSkipped));
    }

    public int Failed
    {
        get => _failed;
        init => _failed = ValidateCounter(value, nameof(Failed));
    }

    public int Rejected
    {
        get => _rejected;
        init => _rejected = ValidateCounter(value, nameof(Rejected));
    }

    private static int ValidateCounter(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Summary counters must be greater than or equal to zero.");
        }

        return value;
    }
}
