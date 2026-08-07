using VSP.Core.Security;
using Xunit;

namespace VSP.Tests.Core.Security;

public class PasswordHasherTests
{
    [Fact]
    public void Verify_WithCorrectPassword_ReturnsTrue()
    {
        var (hash, salt, iterations) = PasswordHasher.Hash("correct-password");

        Assert.True(PasswordHasher.Verify("correct-password", hash, salt, iterations));
    }

    [Fact]
    public void Verify_WithWrongPassword_ReturnsFalse()
    {
        var (hash, salt, iterations) = PasswordHasher.Hash("correct-password");

        Assert.False(PasswordHasher.Verify("wrong-password", hash, salt, iterations));
    }

    [Fact]
    public void Hash_CalledTwiceWithSamePassword_ProducesDifferentSaltAndHash()
    {
        var first = PasswordHasher.Hash("same-password");
        var second = PasswordHasher.Hash("same-password");

        Assert.NotEqual(first.Salt, second.Salt);
        Assert.NotEqual(first.Hash, second.Hash);
    }

    [Fact]
    public void Hash_ReturnsDefaultIterations()
    {
        var (_, _, iterations) = PasswordHasher.Hash("password");

        Assert.Equal(PasswordHasher.DefaultIterations, iterations);
    }

    [Theory]
    [InlineData("")]
    [InlineData("密碼éü🎉")]
    [InlineData("a-very-long-password-that-goes-on-and-on-and-on-and-on-and-on-and-on-and-on-and-on-and-on-and-on")]
    public void Hash_UnusualInputs_DoesNotThrowAndRoundTrips(string password)
    {
        var (hash, salt, iterations) = PasswordHasher.Hash(password);

        Assert.True(PasswordHasher.Verify(password, hash, salt, iterations));
    }
}
