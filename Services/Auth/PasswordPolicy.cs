namespace ltwnc.Services.Auth;

// Chính sách mật khẩu giữ nguyên như Identity options cũ: >=8 ký tự, có số, hoa, thường.
public static class PasswordPolicy
{
    public const int RequiredLength = 8;

    public static AuthError? GetValidationError(string? password)
    {
        // 1. Kiểm tra `string.IsNullOrEmpty(password) || password.Length < RequiredLength` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrEmpty(password) || password.Length < RequiredLength)
        {
            // 2. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new AuthError("PasswordTooShort", "Mật khẩu phải có ít nhất 8 ký tự.");
        }

        // 3. Kiểm tra `!password.Any(char.IsDigit)` để chọn nhánh xử lý phù hợp.
        if (!password.Any(char.IsDigit))
        {
            // 4. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new AuthError("PasswordRequiresDigit", "Mật khẩu phải có ít nhất một chữ số.");
        }

        // 5. Kiểm tra `!password.Any(char.IsUpper)` để chọn nhánh xử lý phù hợp.
        if (!password.Any(char.IsUpper))
        {
            // 6. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new AuthError("PasswordRequiresUpper", "Mật khẩu phải có ít nhất một chữ hoa.");
        }

        // 7. Kiểm tra `!password.Any(char.IsLower)` để chọn nhánh xử lý phù hợp.
        if (!password.Any(char.IsLower))
        {
            // 8. Tạo và trả đối tượng kết quả cho nơi gọi.
            return new AuthError("PasswordRequiresLower", "Mật khẩu phải có ít nhất một chữ thường.");
        }

        // 9. Trả `null` cho nơi gọi.
        return null;
    }
}
