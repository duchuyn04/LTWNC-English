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

    public static bool IsValid(string? username) =>
        GetValidationError(username) is null;

    public static string? GetValidationError(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return "Username không được để trống.";
        }

        string candidate = username.Trim();
        if (candidate.Length is < MinimumLength or > MaximumLength)
        {
            return $"Username phải có từ {MinimumLength}-{MaximumLength} ký tự.";
        }

        if (!IsAsciiLetterOrDigit(candidate[0]) ||
            !IsAsciiLetterOrDigit(candidate[^1]))
        {
            return "Username phải bắt đầu và kết thúc bằng chữ không dấu hoặc số.";
        }

        if (candidate.Any(character =>
                !IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-'))
        {
            return "Username chỉ được chứa chữ không dấu, số, dấu chấm, gạch dưới và gạch ngang.";
        }

        return ReservedUsernames.Contains(candidate)
            ? "Username này được dành riêng cho hệ thống."
            : null;
    }

    private static bool IsAsciiLetterOrDigit(char character) =>
        character is >= 'a' and <= 'z' or
            >= 'A' and <= 'Z' or
            >= '0' and <= '9';
}
