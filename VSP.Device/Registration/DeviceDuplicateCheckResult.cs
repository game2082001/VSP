namespace VSP.Device.Registration;

public sealed class DeviceDuplicateCheckResult
{
    public DeviceDuplicateCheckResult(
        string fieldName,
        string code,
        string message,
        Guid duplicateCameraId)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            throw new ArgumentException("FieldName must not be empty.", nameof(fieldName));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Code must not be empty.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message must not be empty.", nameof(message));
        }

        FieldName = fieldName;
        Code = code;
        Message = message;
        DuplicateCameraId = duplicateCameraId;
    }

    public string FieldName { get; }

    public string Code { get; }

    public string Message { get; }

    public Guid DuplicateCameraId { get; }
}
