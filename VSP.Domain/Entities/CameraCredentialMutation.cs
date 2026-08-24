namespace VSP.Domain.Entities;

public enum CameraCredentialMutationKind
{
    Unchanged,
    Replace,
    Clear
}

public sealed class CameraCredentialMutation
{
    private CameraCredentialMutation(CameraCredentialMutationKind kind, string password)
    {
        Kind = kind;
        Password = password;
    }

    public CameraCredentialMutationKind Kind { get; }

    public string Password { get; }

    public static CameraCredentialMutation Unchanged() => new(CameraCredentialMutationKind.Unchanged, "");

    public static CameraCredentialMutation Replace(string password) => new(CameraCredentialMutationKind.Replace, password ?? "");

    public static CameraCredentialMutation Clear() => new(CameraCredentialMutationKind.Clear, "");
}
