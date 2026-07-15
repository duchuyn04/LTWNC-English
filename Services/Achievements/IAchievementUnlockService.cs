
namespace ltwnc.Services.Achievements;

// So metric với catalog, chèn UserAchievement còn thiếu; trả về danh sách vừa mở lần này.
public interface IAchievementUnlockService
{
    Task<IReadOnlyList<AchievementCatalog.Definition>> SyncEligibleAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
