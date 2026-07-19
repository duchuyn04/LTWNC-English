
namespace ltwnc.Services.Achievements;

// Đếm metric thành tích (thẻ thuộc, buổi học, nghe chép…) một lần cho một user.
public interface IAchievementProgressService
{
    // Đếm toàn bộ metric cho một user và trả snapshot có giá trị 0 khi chưa có hoạt động.
    Task<AchievementProgressSnapshot> GetSnapshotAsync(
        string userId,
        CancellationToken cancellationToken = default);

    // Đếm metric cho nhiều user trong số truy vấn cố định để các trang danh sách không tạo N+1.
    Task<IReadOnlyDictionary<string, AchievementProgressSnapshot>> GetSnapshotsAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default);
}
