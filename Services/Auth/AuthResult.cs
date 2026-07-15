namespace ltwnc.Services.Auth;

// Kết quả Register/Login — map sang ModelState ở controller.
public sealed class AuthResult
{
    public bool Succeeded { get; init; }

    // Danh sách lỗi hiển thị form (không chứa password thô)
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public static AuthResult Success() => new() { Succeeded = true };

    public static AuthResult Failure(params string[] errors) =>
        new() { Succeeded = false, Errors = errors };
}
