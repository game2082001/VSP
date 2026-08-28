namespace VSP.Core.Security;

public static class UsernameIdentity
{
    public static string Normalize(string username)
    {
        ArgumentNullException.ThrowIfNull(username);
        return username.Trim().ToUpperInvariant();
    }
}
