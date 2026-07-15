using System.Text.RegularExpressions;
using ltwnc.Data;
using ltwnc.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services.Auth;

// Đăng ký / đăng nhập / đăng xuất — hash mật khẩu + cookie qua ISignInService.
public class AuthService : IAuthService
{
    private static readonly Regex UppercaseRegex = new("[A-Z]", RegexOptions.Compiled);
    private static readonly Regex LowercaseRegex = new("[a-z]", RegexOptions.Compiled);
    private static readonly Regex DigitRegex = new(@"\d", RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ISignInService _signIn;

    public AuthService(AppDbContext db, IPasswordHasher hasher, ISignInService signIn)
    {
        _db = db;
        _hasher = hasher;
        _signIn = signIn;
    }

    // Task 3 sẽ thêm DbSet Users; tạm dùng Set<AppUser>() để Task 2 build độc lập.
    private DbSet<AppUser> Users => _db.Set<AppUser>();

    public async Task<AuthResult> RegisterAsync(string userName, string email, string password)
    {
        List<string> errors = ValidateRegistration(userName, email, password);
        if (errors.Count > 0)
        {
            return AuthResult.Failure(errors.ToArray());
        }

        string normalizedEmail = NormalizeEmail(email);
        string trimmedUserName = userName.Trim();

        bool emailExists = await Users.AnyAsync(u => u.Email == normalizedEmail);
        if (emailExists)
        {
            return AuthResult.Failure("Email đã được sử dụng.");
        }

        bool userNameExists = await Users.AnyAsync(u => u.UserName == trimmedUserName);
        if (userNameExists)
        {
            return AuthResult.Failure("Tên đăng nhập đã được sử dụng.");
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = trimmedUserName,
            Email = normalizedEmail,
            PasswordHash = _hasher.Hash(password),
            CreatedAt = DateTime.UtcNow
        };

        Users.Add(user);
        await _db.SaveChangesAsync();

        // Giống post-register cũ: cookie persistent 1 ngày
        await _signIn.SignInAsync(user, rememberMe: true, cookieLifetime: TimeSpan.FromDays(1));

        return AuthResult.Success();
    }

    public async Task<AuthResult> LoginAsync(string email, string password, bool rememberMe)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return AuthResult.Failure("Email hoặc mật khẩu không đúng.");
        }

        string normalizedEmail = NormalizeEmail(email);
        AppUser? user = await Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user is null || !_hasher.Verify(password, user.PasswordHash))
        {
            return AuthResult.Failure("Email hoặc mật khẩu không đúng.");
        }

        TimeSpan lifetime = rememberMe ? TimeSpan.FromDays(30) : TimeSpan.FromDays(1);
        await _signIn.SignInAsync(user, rememberMe, lifetime);

        return AuthResult.Success();
    }

    public Task LogoutAsync() => _signIn.SignOutAsync();

    private static string NormalizeEmail(string email) =>
        email.Trim().ToLowerInvariant();

    private static List<string> ValidateRegistration(string userName, string email, string password)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(userName))
        {
            errors.Add("Tên đăng nhập không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            errors.Add("Email không được để trống.");
        }

        if (string.IsNullOrEmpty(password))
        {
            errors.Add("Mật khẩu không được để trống.");
        }
        else
        {
            if (password.Length < 8)
            {
                errors.Add("Mật khẩu phải có ít nhất 8 ký tự.");
            }

            if (!UppercaseRegex.IsMatch(password))
            {
                errors.Add("Mật khẩu phải có ít nhất một chữ hoa.");
            }

            if (!LowercaseRegex.IsMatch(password))
            {
                errors.Add("Mật khẩu phải có ít nhất một chữ thường.");
            }

            if (!DigitRegex.IsMatch(password))
            {
                errors.Add("Mật khẩu phải có ít nhất một chữ số.");
            }
        }

        return errors;
    }
}
