using HexArch.Data.Helpers;

namespace test;

public class PasswordHasherTests
{
    private const string Password = "ClaveSegura1!";

    [Fact]
    public void Hash_NeverReturnsThePlainTextPassword()
    {
        string hash = PasswordHasher.Hash(Password);

        Assert.NotEqual(Password, hash);
        Assert.DoesNotContain(Password, hash, StringComparison.Ordinal);
    }

    [Fact]
    public void Hash_UsesTheIterationsSaltAndKeyFormat()
    {
        string hash = PasswordHasher.Hash(Password);

        string[] parts = hash.Split('.');

        Assert.Equal(3, parts.Length);
        Assert.True(int.TryParse(parts[0], out int iterations));
        Assert.True(iterations >= 100_000);
        Assert.NotEmpty(Convert.FromBase64String(parts[1]));
        Assert.Equal(32, Convert.FromBase64String(parts[2]).Length);
    }

    [Fact]
    public void Hash_UsesAFreshSaltForEveryCall()
    {
        string first = PasswordHasher.Hash(Password);
        string second = PasswordHasher.Hash(Password);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Verify_AcceptsThePasswordTheHashWasBuiltFrom()
    {
        Assert.True(PasswordHasher.Verify(Password, PasswordHasher.Hash(Password)));
    }

    [Theory]
    [InlineData("ClaveSegura2!")]
    [InlineData("clavesegura1!")]
    [InlineData("")]
    public void Verify_RejectsAnyOtherPassword(string candidate)
    {
        Assert.False(PasswordHasher.Verify(candidate, PasswordHasher.Hash(Password)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("100000")]
    [InlineData("100000.abc.def")]
    [InlineData("abc.c2FsdA==.a2V5")]
    [InlineData("100000.!!!notBase64!!!.a2V5")]
    public void Verify_RejectsMalformedHashesInsteadOfThrowing(string hash)
    {
        Assert.False(PasswordHasher.Verify(Password, hash));
    }

    [Fact]
    public void Verify_DoesNotAcceptAPlainTextPasswordAsHash()
    {
        Assert.False(PasswordHasher.Verify(Password, Password));
    }
}