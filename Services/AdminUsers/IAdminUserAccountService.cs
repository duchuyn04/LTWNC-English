namespace ltwnc.Services.AdminUsers;

public interface IAdminUserAccountService
{
    // Tìm danh sách tài khoản theo bộ lọc của trang Admin/Users.
    Task<AdminUserAccountPage> SearchAsync(
        AdminUserAccountQuery query,
        CancellationToken cancellationToken = default);

    // Lấy thông tin chi tiết chỉ đọc của một tài khoản.
    Task<AdminUserAccountDetails?> GetDetailsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    // Khóa tài khoản và thu hồi phiên đăng nhập hiện có.
    Task<AdminUserOperationResult> LockAsync(
        AdminUserAccountCommand command,
        CancellationToken cancellationToken = default);

    // Mở khóa tài khoản nhưng giữ nguyên dữ liệu học tập và nội dung.
    Task<AdminUserOperationResult> UnlockAsync(
        AdminUserAccountCommand command,
        CancellationToken cancellationToken = default);

    // Thu hồi phiên đăng nhập mà không thay đổi trạng thái khóa.
    Task<AdminUserOperationResult> RevokeSessionsAsync(
        AdminUserAccountCommand command,
        CancellationToken cancellationToken = default);
}
