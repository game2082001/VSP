using System.Buffers.Binary;
using System.Security.Cryptography;

namespace VSP.Infrastructure.Security;

internal sealed class CredentialProtectionEnvelope
{
    private static readonly byte[] Magic = "VSPC"u8.ToArray();
    private const byte CurrentVersion = 1;
    private const byte DpapiProvider = 1;
    private const byte CurrentUserScope = 1;
    private const int HeaderLength = 12;

    private CredentialProtectionEnvelope(byte[] ciphertext)
    {
        Ciphertext = ciphertext;
    }

    public byte[] Ciphertext { get; }

    public static byte[] Create(byte[] ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        if (ciphertext.Length == 0)
        {
            throw new ArgumentException("Ciphertext must not be empty.", nameof(ciphertext));
        }

        var envelope = new byte[HeaderLength + ciphertext.Length];
        Magic.CopyTo(envelope, 0);
        envelope[4] = CurrentVersion;
        envelope[5] = DpapiProvider;
        envelope[6] = CurrentUserScope;
        envelope[7] = 0;
        BinaryPrimitives.WriteInt32LittleEndian(envelope.AsSpan(8, 4), ciphertext.Length);
        ciphertext.CopyTo(envelope, HeaderLength);
        return envelope;
    }

    public static CredentialProtectionEnvelope Parse(byte[] envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.Length < HeaderLength || !envelope.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            throw new CryptographicException("The credential protection envelope is invalid.");
        }

        if (envelope[4] != CurrentVersion || envelope[5] != DpapiProvider || envelope[6] != CurrentUserScope || envelope[7] != 0)
        {
            throw new CryptographicException("The credential protection envelope uses an unsupported protection format.");
        }

        var ciphertextLength = BinaryPrimitives.ReadInt32LittleEndian(envelope.AsSpan(8, 4));
        if (ciphertextLength <= 0 || ciphertextLength != envelope.Length - HeaderLength)
        {
            throw new CryptographicException("The credential protection envelope length is invalid.");
        }

        return new CredentialProtectionEnvelope(envelope.AsSpan(HeaderLength).ToArray());
    }
}
