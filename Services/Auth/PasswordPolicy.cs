namespace ltwnc.Services.Auth;

// Chính sách mật khẩu giữ nguyên như Identity options cũ: >=8 ký tự, có số, hoa, thường.
public static class PasswordPolicy
{
    public const int RequiredLength = 8;

    public static AuthError? GetValidationError(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < RequiredLength)
        {
            return new AuthError("PasswordTooShort", "Mật khẩu phải có ít nhất 8 ký tự.");
        }

        if (!password.Any(char.IsDigit))
        {
            return new AuthError("PasswordRequiresDigit", "Mật khẩu phải có ít nhất một chữ số.");
        }

        if (!password.Any(char.IsUpper))
        {
            return new AuthError("PasswordRequiresUpper", "Mật khẩu phải có ít nhất một chữ hoa.");
        }

        if (!password.Any(char.IsLower))
        {
            return new AuthError("PasswordRequiresLower", "Mật khẩu phải có ít nhất một chữ thường.");
        }

        return null;
    }
}
