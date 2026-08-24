namespace VSP.Infrastructure.Security;

public interface ICameraCredentialProtector
{
    byte[] Protect(string plaintext);

    string Unprotect(byte[] protectedEnvelope);
}
