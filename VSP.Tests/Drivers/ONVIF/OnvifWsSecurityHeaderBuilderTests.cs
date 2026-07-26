using System.Security.Cryptography;
using System.Text;
using VSP.Device.Drivers.ONVIF;
using Xunit;

namespace VSP.Tests.Drivers.ONVIF;

public class OnvifWsSecurityHeaderBuilderTests
{
    [Fact]
    public void Build_IncludesSuppliedUsername()
    {
        var header = OnvifWsSecurityHeaderBuilder.Build("admin", "secret");

        var username = header.Descendants().First(e => e.Name.LocalName == "Username").Value;
        Assert.Equal("admin", username);
    }

    [Fact]
    public void Build_PasswordDigestMatchesUsernameTokenProfileAlgorithm()
    {
        var header = OnvifWsSecurityHeaderBuilder.Build("admin", "secret");

        var nonceBase64 = header.Descendants().First(e => e.Name.LocalName == "Nonce").Value;
        var created = header.Descendants().First(e => e.Name.LocalName == "Created").Value;
        var actualDigest = header.Descendants().First(e => e.Name.LocalName == "Password").Value;

        var nonceBytes = Convert.FromBase64String(nonceBase64);
        var createdBytes = Encoding.UTF8.GetBytes(created);
        var passwordBytes = Encoding.UTF8.GetBytes("secret");
        var combined = nonceBytes.Concat(createdBytes).Concat(passwordBytes).ToArray();
        var expectedDigest = Convert.ToBase64String(SHA1.HashData(combined));

        Assert.Equal(expectedDigest, actualDigest);
    }

    [Fact]
    public void Build_NonceIsSixteenRandomBytes()
    {
        var header = OnvifWsSecurityHeaderBuilder.Build("admin", "secret");

        var nonceBase64 = header.Descendants().First(e => e.Name.LocalName == "Nonce").Value;
        var nonceBytes = Convert.FromBase64String(nonceBase64);

        Assert.Equal(16, nonceBytes.Length);
    }

    [Fact]
    public void Build_GeneratesDifferentNonceAndDigestEachCall()
    {
        var first = OnvifWsSecurityHeaderBuilder.Build("admin", "secret");
        var second = OnvifWsSecurityHeaderBuilder.Build("admin", "secret");

        var firstNonce = first.Descendants().First(e => e.Name.LocalName == "Nonce").Value;
        var secondNonce = second.Descendants().First(e => e.Name.LocalName == "Nonce").Value;

        Assert.NotEqual(firstNonce, secondNonce);
    }

    [Fact]
    public void Build_WithNullOrWhitespaceUsername_Throws()
    {
        Assert.Throws<ArgumentException>(() => OnvifWsSecurityHeaderBuilder.Build(" ", "secret"));
    }
}
