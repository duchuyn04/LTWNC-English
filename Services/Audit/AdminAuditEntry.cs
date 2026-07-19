namespace ltwnc.Services.Audit;

public static class AdminAuditActions
{
    public const string AdminAreaSignIn = "AdminArea.SignIn";
    public const string UsersLock = "Users.Lock";
    public const string UsersUnlock = "Users.Unlock";
    public const string UsersRevokeSessions = "Users.RevokeSessions";
}

public static class AdminAuditOutcome
{
    public const string Success = "Success";
    public const string Failure = "Failure";
    public const string Denied = "Denied";
}

// Dữ liệu đầu vào cho một Bản ghi kiểm toán quản trị.
public sealed record AdminAuditEntry(
    string ActorUserId,
    string ActorDisplay,
    string Action,
    string Outcome,
    string? TargetType = null,
    string? TargetId = null,
    string? Reason = null,
    string? CorrelationId = null,
    IReadOnlyDictionary<string, string?>? Metadata = null);

public sealed record AdminAuditQuery(
    string? Search = null,
    string? Action = null,
    string? Outcome = null,
    int Page = 1,
    int PageSize = AdminAuditService.DefaultPageSize);

public sealed record AdminAuditLogPage(
    IReadOnlyList<ltwnc.Models.Entities.AdminAuditLog> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => TotalCount == 0
        ? 1
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
