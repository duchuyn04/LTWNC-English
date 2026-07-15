using ltwnc.Services.StudyEvents;

namespace ltwnc.Services;

// Đếm metric thành tích (thẻ thuộc, buổi học, nghe chép…) một lần cho một user.
public interface IAchievementProgressService
{
    Task<AchievementProgressSnapshot> GetSnapshotAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
