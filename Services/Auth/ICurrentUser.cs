namespace ltwnc.Services.Auth;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    // ClaimTypes.NameIdentifier
    string? UserId { get; }

    string? UserName { get; }
}
