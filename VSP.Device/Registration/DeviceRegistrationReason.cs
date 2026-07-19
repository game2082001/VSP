namespace VSP.Device.Registration;

public sealed class DeviceRegistrationReason
{
    public DeviceRegistrationReason(string code, string message)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Code must not be empty.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message must not be empty.", nameof(message));
        }

        Code = code;
        Message = message;
    }

    public string Code { get; }

    public string Message { get; }
}
