namespace ltwnc.Services.Auth;

public sealed record AuthError(string Code, string Message);

public sealed class AuthResult
{
    public bool Succeeded { get; private init; }
    public bool IsLockedOut { get; private init; }
    public IReadOnlyList<AuthError> Errors { get; private init; } = [];

    public static AuthResult Success() => new() { Succeeded = true };
    public static AuthResult LockedOut() => new() { IsLockedOut = true };
    public static AuthResult Failure(params AuthError[] errors) => new() { Errors = errors };
}
