namespace VSP.Device.Users;

public sealed record UserLifecycleResult(bool Success, string? FailureMessage = null)
{
    public static UserLifecycleResult Ok() => new(true);

    public static UserLifecycleResult Failed(string failureMessage) => new(false, failureMessage);
}
