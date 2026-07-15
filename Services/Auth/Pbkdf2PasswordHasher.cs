using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace ltwnc.Services.Auth;

// PBKDF2 versioned: v1.{iterations}.{salt}.{hash}
public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Version = "v1";
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] subkey = KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: Iterations,
            numBytesRequested: KeySize);

        return $"{Version}.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(subkey)}";
    }

    public bool Verify(string password, string passwordHash)
    {
        if (password is null || passwordHash is null)
        {
            return false;
        }

        string[] parts = passwordHash.Split('.');
        if (parts.Length != 4)
        {
            return false;
        }

        if (!string.Equals(parts[0], Version, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out int iterations) || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedSubkey;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedSubkey = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length == 0 || expectedSubkey.Length == 0)
        {
            return false;
        }

        byte[] actualSubkey = KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: iterations,
            numBytesRequested: expectedSubkey.Length);

        return CryptographicOperations.FixedTimeEquals(actualSubkey, expectedSubkey);
    }
}
