namespace ltwnc.Services.Profiles;

public static class UsernamePolicy
{
    public const int MinimumLength = 3;
    public const int MaximumLength = 50;
    public const string AllowedIdentityCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._-";

    private static readonly HashSet<string> ReservedUsernames = new(
        [
            "account",
            "set",
            "study",
            "achievements",
            "api",
            "flashcardset",
            "cards",
            "cardactions",
            "home",
            "profile",
            "u",
            "css",
            "js",
            "lib",
            "images",
            "uploads",
            "favicon.ico"
        ],
        StringComparer.OrdinalIgnoreCase);

    public static bool IsValid(string? username)
    {
        // 1. Trả `GetValidationError(username) is null` cho nơi gọi.
        return GetValidationError(username) is null;
    }

    public static string? GetValidationError(string? username)
    {
        // 1. Kiểm tra `string.IsNullOrWhiteSpace(username)` để chọn nhánh xử lý phù hợp.
        if (string.IsNullOrWhiteSpace(username))
        {
            // 2. Trả `"Username không được để trống."` cho nơi gọi.
            return "Username không được để trống.";
        }

        // 3. Gọi `Trim` và lưu kết quả vào `candidate`.
        string candidate = username.Trim();
        // 4. Kiểm tra `candidate.Length is < MinimumLength or > MaximumLength` để chọn nhánh xử lý phù hợp.
        if (candidate.Length is < MinimumLength or > MaximumLength)
        {
            // 5. Trả `$"Username phải có từ {MinimumLength}-{MaximumLength} ký tự."` cho nơi gọi.
            return $"Username phải có từ {MinimumLength}-{MaximumLength} ký tự.";
        }

        // 6. Kiểm tra `!IsAsciiLetterOrDigit(candidate[0]) || !IsAsciiLetterOrDigit(candid...` để chọn nhánh xử lý phù hợp.
        if (!IsAsciiLetterOrDigit(candidate[0]) ||
            !IsAsciiLetterOrDigit(candidate[^1]))
        {
            // 7. Trả `"Username phải bắt đầu và kết thúc bằng chữ không dấu hoặc số."` cho nơi gọi.
            return "Username phải bắt đầu và kết thúc bằng chữ không dấu hoặc số.";
        }

        // 8. Kiểm tra `candidate.Any(character => !IsAsciiLetterOrDigit(character) && char...` để chọn nhánh xử lý phù hợp.
        if (candidate.Any(character =>
                !IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-'))
        {
            // 9. Trả `"Username chỉ được chứa chữ không dấu, số, dấu chấm, gạch dưới và g...` cho nơi gọi.
            return "Username chỉ được chứa chữ không dấu, số, dấu chấm, gạch dưới và gạch ngang.";
        }

        // 10. Trả `ReservedUsernames.Contains(candidate) ? "Username này được dành riê...` cho nơi gọi.
        return ReservedUsernames.Contains(candidate)
            ? "Username này được dành riêng cho hệ thống."
            : null;
    }

    private static bool IsAsciiLetterOrDigit(char character)
    {
        // 1. Trả `character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <...` cho nơi gọi.
        return character is >= 'a' and <= 'z' or
            >= 'A' and <= 'Z' or
            >= '0' and <= '9';
    }
}
