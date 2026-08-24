namespace VSP.Domain.Entities;

public sealed class CameraCredentials
{
    public CameraCredentials(string username, string password)
    {
        Username = username ?? "";
        Password = password ?? "";
    }

    public string Username { get; }

    public string Password { get; }
}
