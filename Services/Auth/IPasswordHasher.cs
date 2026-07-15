namespace ltwnc.Services.Auth;

// Hash và verify mật khẩu (PBKDF2), không dùng ASP.NET Identity.
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
