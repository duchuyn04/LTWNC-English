using ltwnc.Data;
using ltwnc.Models.ViewModels.Achievements;
using ltwnc.Services.StudyEvents;
using Microsoft.EntityFrameworkCore;

namespace ltwnc.Services;

// Dữ liệu trang Thành tích: list huy hiệu + tiêu đề vừa mở khi rescan
public sealed class AchievementPageModel
{
    // Từng huy hiệu kèm tiến độ / trạng thái mở
    public IReadOnlyList<AchievementListItemViewModel> Items { get; init; } =
        Array.Empty<AchievementListItemViewModel>();

    // Title các huy hiệu vừa mở trong lần rescan này (hiện banner TempData)
    public IReadOnlyList<string> NewlyUnlockedTitles { get; init; } =
        Array.Empty<string>();
}

// Đọc trang Thành tích: rescan unlock, ghép catalog + metric + DB đã mở.
public class AchievementService
{
    // Đọc UserAchievements đã lưu
    private readonly AppDbContext _context;

    // Rescan mở huy hiệu đủ điều kiện còn thiếu
    private readonly AchievementUnlockService _unlock;

    // Lấy metric cho progress bar
    private readonly AchievementProgressService _progress;

    // Inject unlock, progress và DbContext
    public AchievementService(
        AppDbContext context,
        AchievementUnlockService unlock,
        AchievementProgressService progress)
    {
        _context = context;
        _unlock = unlock;
        _progress = progress;
    }

    // Rescan + snapshot + map catalog thành view model đã sắp xếp
    public async Task<AchievementPageModel> GetPageAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // 1. Bù huy hiệu đủ mốc nhưng chưa có dòng DB
        IReadOnlyList<AchievementCatalog.Definition> newlyUnlockedDefinitions =
            await _unlock.SyncEligibleAsync(userId, cancellationToken);

        // 2. Metric hiện tại cho progress bar
        AchievementProgressSnapshot snapshot =
            await _progress.GetSnapshotAsync(userId, cancellationToken);

        // 3. Code đã mở sau rescan: code -> thời điểm mở
        Dictionary<string, DateTime> unlockedByCode = await _context.UserAchievements
            .AsNoTracking()
            .Where(achievement => achievement.UserId == userId)
            .ToDictionaryAsync(
                achievement => achievement.Code,
                achievement => achievement.UnlockedAt,
                cancellationToken);

        // 4. Mỗi definition trong catalog -> một dòng UI
        List<AchievementListItemViewModel> items = new();

        foreach (AchievementCatalog.Definition definition in AchievementCatalog.All)
        {
            int metricValue = snapshot.GetValue(definition.Metric);
            int cappedCurrent = Math.Min(metricValue, definition.Target);
            bool isUnlocked = unlockedByCode.ContainsKey(definition.Code);

            DateTime? unlockedAt = null;
            if (isUnlocked)
            {
                unlockedAt = unlockedByCode[definition.Code];
            }

            // Đã mở: current = target, 100%. Chưa mở: current capped, % theo target
            int displayCurrent;
            int progressPercent;
            if (isUnlocked)
            {
                displayCurrent = definition.Target;
                progressPercent = 100;
            }
            else if (definition.Target <= 0)
            {
                displayCurrent = cappedCurrent;
                progressPercent = 0;
            }
            else
            {
                displayCurrent = cappedCurrent;
                progressPercent = cappedCurrent * 100 / definition.Target;
            }

            items.Add(new AchievementListItemViewModel
            {
                Code = definition.Code,
                Title = definition.Title,
                Description = definition.Description,
                IsUnlocked = isUnlocked,
                UnlockedAt = unlockedAt,
                Current = displayCurrent,
                Target = definition.Target,
                ProgressPercent = progressPercent,
                CtaText = definition.CtaText,
                CtaUrl = definition.CtaPath
            });
        }

        // Ưu tiên đã mở, rồi % cao, rồi title
        items = items
            .OrderByDescending(item => item.IsUnlocked)
            .ThenByDescending(item => item.ProgressPercent)
            .ThenBy(item => item.Title)
            .ToList();

        List<string> newlyUnlockedTitles = newlyUnlockedDefinitions
            .Select(definition => definition.Title)
            .ToList();

        return new AchievementPageModel
        {
            Items = items,
            NewlyUnlockedTitles = newlyUnlockedTitles
        };
    }
}
