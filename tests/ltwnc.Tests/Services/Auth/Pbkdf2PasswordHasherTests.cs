using ltwnc.Services.Auth;
using Xunit;

namespace ltwnc.Tests.Services.Auth;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_then_Verify_succeeds_for_same_password()
    {
        string hash = _hasher.Hash("Secret1A");
        Assert.True(_hasher.Verify("Secret1A", hash));
    }

    [Fact]
    public void Verify_fails_for_wrong_password()
    {
        string hash = _hasher.Hash("Secret1A");
        Assert.False(_hasher.Verify("Wrong999", hash));
    }

    [Fact]
    public void Verify_fails_for_garbage_hash_string()
    {
        Assert.False(_hasher.Verify("Secret1A", "not-a-valid-hash"));
    }

    [Fact]
    public void Hash_produces_v1_prefixed_payload()
    {
        string hash = _hasher.Hash("Secret1A");
        Assert.StartsWith("v1.", hash);
    }
}
