using System.Security.Cryptography;
using System.Text;

namespace VSP.Infrastructure.Security;

public sealed class DpapiCurrentUserCameraCredentialProtector : ICameraCredentialProtector
{
    private static readonly byte[] OptionalEntropy = "VSP.CameraCredential.v1"u8.ToArray();
    private readonly IDpapiProtectionProvider _dpapi;

    public DpapiCurrentUserCameraCredentialProtector()
        : this(new WindowsDpapiProtectionProvider())
    {
    }

    internal DpapiCurrentUserCameraCredentialProtector(IDpapiProtectionProvider dpapi)
    {
        _dpapi = dpapi ?? throw new ArgumentNullException(nameof(dpapi));
    }

    public byte[] Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        try
        {
            var ciphertext = _dpapi.Protect(plaintextBytes, OptionalEntropy, DataProtectionScope.CurrentUser);
            try
            {
                return CredentialProtectionEnvelope.Create(ciphertext);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(ciphertext);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    public string Unprotect(byte[] protectedEnvelope)
    {
        var envelope = CredentialProtectionEnvelope.Parse(protectedEnvelope);
        var plaintextBytes = _dpapi.Unprotect(
            envelope.Ciphertext,
            OptionalEntropy,
            DataProtectionScope.CurrentUser);

        try
        {
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }
}

internal interface IDpapiProtectionProvider
{
    byte[] Protect(byte[] plaintext, byte[] optionalEntropy, DataProtectionScope scope);

    byte[] Unprotect(byte[] ciphertext, byte[] optionalEntropy, DataProtectionScope scope);
}

internal sealed class WindowsDpapiProtectionProvider : IDpapiProtectionProvider
{
    public byte[] Protect(byte[] plaintext, byte[] optionalEntropy, DataProtectionScope scope) =>
        ProtectedData.Protect(plaintext, optionalEntropy, scope);

    public byte[] Unprotect(byte[] ciphertext, byte[] optionalEntropy, DataProtectionScope scope) =>
        ProtectedData.Unprotect(ciphertext, optionalEntropy, scope);
}
