namespace ltwnc.Services.Audit;

public static class AdminAuditActions
{
    public const string AdminAreaSignIn = "AdminArea.SignIn";
    public const string UsersLock = "Users.Lock";
    public const string UsersUnlock = "Users.Unlock";
    public const string UsersRevokeSessions = "Users.RevokeSessions";
    public const string ContentReportsDismiss = "ContentReports.Dismiss";
    public const string ContentReportsQuarantine = "ContentReports.Quarantine";
    public const string ContentSetsRestore = "ContentSets.Restore";
    public const string AiProvidersCreate = "AiProviders.Create";
    public const string AiProvidersUpdate = "AiProviders.Update";
    public const string AiProvidersSetPrimary = "AiProviders.SetPrimary";
    public const string AiProvidersDisable = "AiProviders.Disable";
    public const string AiProvidersEnable = "AiProviders.Enable";
    public const string CreditPackagesCreate = "Credits.Packages.Create";
    public const string CreditPackagesUpdate = "Credits.Packages.Update";
    public const string CreditPackagesArchive = "Credits.Packages.Archive";
    public const string CreditPackagesUnarchive = "Credits.Packages.Unarchive";
    public const string CreditBalanceAdjust = "Credits.Balance.Adjust";
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
    string? Outcome = null,
    int Page = 1);

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
