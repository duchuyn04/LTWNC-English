namespace ltwnc.Services.Achievements;

// Dữ liệu trang Thành tích: rescan unlock + map list view model.
// AchievementPageModel vẫn là class concrete (không phải interface).
public interface IAchievementService
{
    Task<AchievementPageModel> GetPageAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
