namespace ltwnc.Services.AdminAchievements;

// Cong truy cap chuc nang quan tri thanh tich: chi doc catalog/ket qua va dong bo lai tu co che hien co.
public interface IAdminAchievementService
{
    // Lay trang tong quan thanh tich cho Admin, gom catalog tu source code va ket qua theo tung nguoi dung.
    Task<AdminAchievementOverview> GetOverviewAsync(
        AdminAchievementQuery query,
        CancellationToken cancellationToken = default);

    // Dong bo lai thanh tich cho mot nguoi dung cu the, khong cap/thu hoi thu cong bat ky ma thanh tich nao.
    Task<AdminAchievementSyncResult> ResyncUserAsync(
        AdminAchievementSyncCommand command,
        CancellationToken cancellationToken = default);

    // Dong bo lai thanh tich toan he thong theo cac lo nho, dung chung rule tinh thanh tich hien co.
    Task<AdminAchievementBatchSyncResult> ResyncAllAsync(
        AdminAchievementBatchSyncCommand command,
        CancellationToken cancellationToken = default);
}
