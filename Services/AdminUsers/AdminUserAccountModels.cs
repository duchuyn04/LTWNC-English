namespace ltwnc.Services.AdminUsers;

public sealed record AdminUserAccountQuery(
    string? Search = null,
    string? Status = null,
    string? Sort = null,
    int Page = AdminUserAccountService.DefaultPage,
    int PageSize = AdminUserAccountService.DefaultPageSize);

public sealed record AdminUserAccountRow(
    string Id,
    string UserName,
    string Email,
    bool EmailConfirmed,
    bool IsAdmin,
    bool IsLocked,
    DateTime? CreatedAtUtc,
    DateTimeOffset? LockoutEnd);

public sealed record AdminUserAccountPage(
    IReadOnlyList<AdminUserAccountRow> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    // Tính tổng trang an toàn để giao diện luôn có ít nhất một trang hiển thị.
    public int TotalPages
    {
        get
        {
            if (TotalCount == 0)
            {
                return 1;
            }

            return (int)Math.Ceiling(TotalCount / (double)PageSize);
        }
    }
}

public sealed record AdminUserAccountDetails(
    string Id,
    string UserName,
    string Email,
    bool EmailConfirmed,
    bool LockoutEnabled,
    bool IsAdmin,
    bool IsLocked,
    int AccessFailedCount,
    string ConcurrencyStamp,
    DateTime? CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTimeOffset? LockoutEnd);

public sealed record AdminUserAccountCommand(
    string ActorUserId,
    string ActorDisplay,
    string TargetUserId,
    string Reason,
    string ConcurrencyStamp,
    string? CorrelationId = null);

public sealed class AdminUserOperationResult
{
    public bool Succeeded { get; init; }
    public string Message { get; init; } = string.Empty;

    // Tạo kết quả thành công với thông báo tiếng Việt cho giao diện.
    public static AdminUserOperationResult Success(string message)
    {
        return new AdminUserOperationResult
        {
            Succeeded = true,
            Message = message
        };
    }

    // Tạo kết quả thất bại với lý do cụ thể để hiển thị và kiểm thử.
    public static AdminUserOperationResult Failure(string message)
    {
        return new AdminUserOperationResult
        {
            Succeeded = false,
            Message = message
        };
    }
}
