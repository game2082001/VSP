using System.Security.Cryptography;
using System.Runtime.InteropServices;
using VSP.Infrastructure.Security;
using Xunit;

namespace VSP.Tests.Infrastructure;

public class CameraCredentialProtectionTests
{
    [Fact]
    public void ProtectAndUnprotect_CurrentWindowsUser_RoundTripsUnicodeSecret()
    {
        var protector = new DpapiCurrentUserCameraCredentialProtector();
        const string secret = "camera-password-\u5bc6\u78bc-\U0001f512";

        var envelope = protector.Protect(secret);

        Assert.Equal(secret, protector.Unprotect(envelope));
        Assert.False(ContainsSequence(envelope, System.Text.Encoding.UTF8.GetBytes(secret)));
    }

    [Fact]
    public void Protect_SameSecretTwice_ProducesDifferentCiphertext()
    {
        var protector = new DpapiCurrentUserCameraCredentialProtector();

        var first = protector.Protect("same-camera-password");
        var second = protector.Protect("same-camera-password");

        Assert.False(first.SequenceEqual(second));
        Assert.Equal("same-camera-password", protector.Unprotect(first));
        Assert.Equal("same-camera-password", protector.Unprotect(second));
    }

    [Fact]
    public void Unprotect_TamperedCiphertext_FailsClosed()
    {
        var protector = new DpapiCurrentUserCameraCredentialProtector();
        var envelope = protector.Protect("tamper-target");
        envelope[^1] ^= 0x5A;

        Assert.Throws<CryptographicException>(() => protector.Unprotect(envelope));
    }

    [Fact]
    public void Unprotect_UnsupportedEnvelopeVersion_FailsBeforeDpapi()
    {
        var provider = new RecordingDpapiProvider();
        var protector = new DpapiCurrentUserCameraCredentialProtector(provider);
        var envelope = protector.Protect("unsupported-version");
        envelope[4] = 99;

        Assert.Throws<CryptographicException>(() => protector.Unprotect(envelope));
        Assert.Equal(0, provider.UnprotectCalls);
    }

    [Fact]
    public void ProtectAndUnprotect_AlwaysRequestCurrentUserScope()
    {
        var provider = new RecordingDpapiProvider();
        var protector = new DpapiCurrentUserCameraCredentialProtector(provider);

        var envelope = protector.Protect("scope-check");
        protector.Unprotect(envelope);

        Assert.Equal(new[] { DataProtectionScope.CurrentUser, DataProtectionScope.CurrentUser }, provider.Scopes);
    }

    [Fact]
    public void Unprotect_WhenDpapiRejectsCallerIdentity_FailsClosedWithoutFallback()
    {
        var provider = new RejectingDpapiProvider();
        var protector = new DpapiCurrentUserCameraCredentialProtector(provider);
        var envelope = CredentialProtectionEnvelope.Create(new byte[] { 1, 2, 3 });

        Assert.Throws<CryptographicException>(() => protector.Unprotect(envelope));
        Assert.Equal(DataProtectionScope.CurrentUser, provider.RequestedScope);
    }

    [Fact]
    public void Unprotect_WhileRunningAsDifferentWindowsPrincipal_FailsClosed()
    {
        var protector = new DpapiCurrentUserCameraCredentialProtector();
        var envelope = protector.Protect("identity-bound-secret");
        Exception? failure = null;
        var unexpectedlyDecrypted = false;

        Assert.True(ImpersonateAnonymousToken(GetCurrentThread()), "Could not establish a different Windows principal for the DPAPI boundary test.");
        try
        {
            try
            {
                protector.Unprotect(envelope);
                unexpectedlyDecrypted = true;
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }
        finally
        {
            Assert.True(RevertToSelf(), "Could not restore the Windows test process identity.");
        }

        Assert.False(unexpectedlyDecrypted);
        Assert.IsType<CryptographicException>(failure);
    }

    private sealed class RecordingDpapiProvider : IDpapiProtectionProvider
    {
        public List<DataProtectionScope> Scopes { get; } = new();
        public int UnprotectCalls { get; private set; }

        public byte[] Protect(byte[] plaintext, byte[] optionalEntropy, DataProtectionScope scope)
        {
            Scopes.Add(scope);
            return plaintext.Select(value => (byte)(value ^ 0xA5)).ToArray();
        }

        public byte[] Unprotect(byte[] ciphertext, byte[] optionalEntropy, DataProtectionScope scope)
        {
            Scopes.Add(scope);
            UnprotectCalls++;
            return ciphertext.Select(value => (byte)(value ^ 0xA5)).ToArray();
        }
    }

    private sealed class RejectingDpapiProvider : IDpapiProtectionProvider
    {
        public DataProtectionScope? RequestedScope { get; private set; }

        public byte[] Protect(byte[] plaintext, byte[] optionalEntropy, DataProtectionScope scope) =>
            throw new NotSupportedException();

        public byte[] Unprotect(byte[] ciphertext, byte[] optionalEntropy, DataProtectionScope scope)
        {
            RequestedScope = scope;
            throw new CryptographicException("The protected data belongs to a different Windows identity.");
        }
    }

    private static bool ContainsSequence(byte[] haystack, byte[] needle)
    {
        return haystack.AsSpan().IndexOf(needle) >= 0;
    }

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentThread();

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImpersonateAnonymousToken(nint threadHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RevertToSelf();
}
